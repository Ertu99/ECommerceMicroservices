using Microsoft.Extensions.Hosting;
using PaymentService.Application.DTOs.Events;
using PaymentService.Application.Interfaces;
using PaymentService.Application.Redis;
using PaymentService.Application.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace PaymentService.Api.HostedServices
{
    public class OrderCreatedConsumer : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public OrderCreatedConsumer(IServiceScopeFactory scopeFactory)
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
                queue: "order_events",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: stoppingToken
            );

            Console.WriteLine("💳 PaymentService OrderCreated eventlerini dinliyor...");

            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += async (sender, ea) =>
            {
                try
                {
                    var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                    var evt = JsonSerializer.Deserialize<OrderCreatedEvent>(json);

                    // Null event korunması
                    if (evt == null)
                    {
                        Console.WriteLine("⚠️ OrderCreated event NULL geldi!");
                        await channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                        return;
                    }

                    using var scope = _scopeFactory.CreateScope();
                    var paymentService = scope.ServiceProvider.GetRequiredService<PaymentAppService>();
                    var cache = scope.ServiceProvider.GetRequiredService<IRedisCacheService>();

                    // ======================================
                    //           IDEMPOTENCY KONTROLÜ
                    // ======================================
                    var idemKey = CacheKeys.PaymentIdempotency(evt.EventId.ToString());
                    var isFirst = await cache.TrySetIdempotencyKeyAsync(idemKey);

                    if (!isFirst)
                    {
                        Console.WriteLine($"⚠️ Duplicate event DROP edildi | EventId={evt.EventId}");
                        await channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                        return;
                    }

                    Console.WriteLine($"🟢 Idempotency OK → İlk event işlendi | EventId={evt.EventId}");

                    // ======================================
                    //          NORMAL ÖDEME AKIŞI
                    // ======================================
                    await paymentService.ProcessPaymentAsync(evt);

                    Console.WriteLine($"💰 Payment işlemi tamamlandı | OrderId={evt.OrderId}");

                    await channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ PaymentService Hatası: {ex.Message}");
                    await channel.BasicNackAsync(ea.DeliveryTag, false, true, stoppingToken);
                }
            };

            await channel.BasicConsumeAsync(
                queue: "order_events",
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken
            );

            // Worker sonsuza kadar çalışacak
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
    }
}
