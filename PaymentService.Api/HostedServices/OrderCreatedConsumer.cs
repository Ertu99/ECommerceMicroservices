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

            // 1) EXCHANGE tanımı
            await channel.ExchangeDeclareAsync(
                exchange: "order_exchange",
                type: "direct",
                durable: true,
                autoDelete: false,
                arguments: null,
                cancellationToken: stoppingToken
            );

            // 2) QUEUE tanımı
            await channel.QueueDeclareAsync(
                queue: "order_events",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: stoppingToken
            );

            // 3) BIND → queue + exchange + routing key
            await channel.QueueBindAsync(
                queue: "order_events",
                exchange: "order_exchange",
                routingKey: "order.created",
                arguments: null,
                cancellationToken: stoppingToken
            );

            Console.WriteLine("💳 PaymentService 'order_events' kuyruğunu (order_exchange/order.created) dinliyor...");

            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += async (sender, ea) =>
            {
                try
                {
                    var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                    var evt = JsonSerializer.Deserialize<OrderCreatedEvent>(json);

                    if (evt == null)
                    {
                        Console.WriteLine("⚠️ OrderCreated event NULL geldi!");
                        await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                        return;
                    }

                    using var scope = _scopeFactory.CreateScope();
                    var paymentService = scope.ServiceProvider.GetRequiredService<PaymentAppService>();
                    var cache = scope.ServiceProvider.GetRequiredService<IRedisCacheService>();

                    // ==============================
                    //      IDEMPOTENCY KONTROLÜ
                    // ==============================
                    var idemKey = CacheKeys.PaymentIdempotency(evt.EventId.ToString());
                    var isFirst = await cache.TrySetIdempotencyKeyAsync(idemKey);

                    if (!isFirst)
                    {
                        Console.WriteLine($"⚠️ Duplicate event DROP edildi | EventId={evt.EventId}");
                        await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                        return;
                    }

                    Console.WriteLine($"🟢 Idempotency OK → İlk event işlendi | EventId={evt.EventId}");

                    // ==============================
                    //        NORMAL ÖDEME AKIŞI
                    // ==============================
                    await paymentService.ProcessPaymentAsync(evt);

                    Console.WriteLine($"💰 Payment işlemi tamamlandı | OrderId={evt.OrderId}");

                    await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ PaymentService Hatası: {ex.Message}");
                    await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true, cancellationToken: stoppingToken);
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
