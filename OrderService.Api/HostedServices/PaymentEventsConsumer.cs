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

            await channel.QueueDeclareAsync(
                queue: "payment_events",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: stoppingToken
            );

            Console.WriteLine("🧾 OrderService → Payment events dinleniyor...");

            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += async (sender, ea) =>
            {
                try
                {
                    string json = Encoding.UTF8.GetString(ea.Body.ToArray());

                    var wrapper = JsonSerializer.Deserialize<PaymentEventWrapper>(json);

                    if (wrapper == null)
                    {
                        Console.WriteLine("⚠ Geçersiz event wrapper alındı.");
                        await channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                        return;
                    }

                    using var scope = _scopeFactory.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<OrderAppService>();

                    switch (wrapper.EventType)
                    {
                        case "PaymentSucceeded":
                            var evtSuccess = JsonSerializer.Deserialize<PaymentSucceededEvent>(wrapper.Payload);
                            if (evtSuccess != null)
                                await service.HandlePaymentSucceededAsync(evtSuccess);
                            break;

                        case "PaymentFailed":
                            var evtFail = JsonSerializer.Deserialize<PaymentFailedEvent>(wrapper.Payload);
                            if (evtFail != null)
                                await service.HandlePaymentFailedAsync(evtFail);
                            break;

                        default:
                            Console.WriteLine($"⚠ Bilinmeyen eventType: {wrapper.EventType}");
                            break;
                    }

                    await channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ PaymentEventConsumer HATASI: {ex.Message}");
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

    // PaymentService'in gönderdiği JSON'u karşılayan model
    public class PaymentEventWrapper
    {
        public string EventType { get; set; } = "";
        public string Payload { get; set; } = "";
    }
}
