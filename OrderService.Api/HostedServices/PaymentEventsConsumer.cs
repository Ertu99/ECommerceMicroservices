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

            await using var channel = await connection.CreateChannelAsync(
                new CreateChannelOptions(
                    publisherConfirmationsEnabled: false,
                    publisherConfirmationTrackingEnabled: false
                ),
                cancellationToken: stoppingToken
            );

            await channel.QueueDeclareAsync(
                queue: "payment_events",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: stoppingToken
            );

            Console.WriteLine("🧾 OrderService Payment eventlerini dinliyor...");

            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += async (sender, ea) =>
            {
                try
                {
                    var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                    var envelope = JsonSerializer.Deserialize<IntegrationEventEnvelope>(json);

                    if (envelope == null)
                    {
                        await channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                        return;
                    }

                    using var scope = _scopeFactory.CreateScope();
                    var orderService = scope.ServiceProvider.GetRequiredService<OrderAppService>();

                    switch (envelope.EventType)
                    {
                        case "PaymentSucceeded":
                            {
                                var evt = JsonSerializer.Deserialize<PaymentSucceededEvent>(envelope.Payload);
                                if (evt != null)
                                    await orderService.HandlePaymentSucceededAsync(evt);
                                break;
                            }

                        case "PaymentFailed":
                            {
                                var evt = JsonSerializer.Deserialize<PaymentFailedEvent>(envelope.Payload);
                                if (evt != null)
                                    await orderService.HandlePaymentFailedAsync(evt);
                                break;
                            }

                        default:
                            Console.WriteLine($"⚠ Bilinmeyen event type: {envelope.EventType}");
                            break;
                    }

                    await channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ PaymentEventsConsumer hata: {ex.Message}");
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
