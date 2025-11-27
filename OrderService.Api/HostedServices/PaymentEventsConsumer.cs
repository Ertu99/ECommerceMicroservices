using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using OrderService.Application.DTOs.Events;
using OrderService.Application.Services;

namespace OrderService.Api.HostedServices
{
    public class PaymentEventsConsumer : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public PaymentEventsConsumer(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var factory = new ConnectionFactory
            {
                HostName = "localhost",
                UserName = "guest",
                Password = "guest"
            };

            await using var connection = await factory.CreateConnectionAsync(stoppingToken);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

            // 1) EXCHANGE DECLARE
            await channel.ExchangeDeclareAsync(
                exchange: "payment_exchange",
                type: "direct",
                durable: true,
                autoDelete: false,
                arguments: null,
                cancellationToken: stoppingToken
            );

            // 2) QUEUE DECLARE
            await channel.QueueDeclareAsync(
                queue: "payment_events",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: stoppingToken
            );

            // 3) BIND: payment.succeeded
            await channel.QueueBindAsync(
                queue: "payment_events",
                exchange: "payment_exchange",
                routingKey: "payment.succeeded",
                arguments: null,
                cancellationToken: stoppingToken
            );

            // 4) BIND: payment.failed
            await channel.QueueBindAsync(
                queue: "payment_events",
                exchange: "payment_exchange",
                routingKey: "payment.failed",
                arguments: null,
                cancellationToken: stoppingToken
            );

            Console.WriteLine("🧾 OrderService → Payment events dinleniyor...");

            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += async (sender, ea) =>
            {
                try
                {
                    var json = Encoding.UTF8.GetString(ea.Body.ToArray());

                    using var scope = _scopeFactory.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<OrderAppService>();

                    if (ea.RoutingKey == "payment.succeeded")
                    {
                        var evt = JsonSerializer.Deserialize<PaymentSucceededEvent>(json);
                        if (evt != null)
                            await service.HandlePaymentSucceededAsync(evt);
                    }
                    else if (ea.RoutingKey == "payment.failed")
                    {
                        var evt = JsonSerializer.Deserialize<PaymentFailedEvent>(json);
                        if (evt != null)
                            await service.HandlePaymentFailedAsync(evt);
                    }
                    else
                    {
                        Console.WriteLine($"⚠ Bilinmeyen routing key: {ea.RoutingKey}");
                    }

                    await channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ PaymentEventsConsumer HATA: {ex.Message}");
                    await channel.BasicNackAsync(ea.DeliveryTag, false, true, stoppingToken);
                }
            };

            await channel.BasicConsumeAsync(
                queue: "payment_events",
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken
            );

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
    }
}
