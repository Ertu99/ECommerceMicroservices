using Dapper;
using Microsoft.Extensions.Hosting;
using PaymentService.Application.DTOs.Events;
using PaymentService.Domain.Entities;
using PaymentService.Infrastructure.Database.Dapper;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace PaymentService.Api.HostedServices
{
    public class PaymentOutboxWorker : BackgroundService
    {
        private readonly DapperContext _context;

        public PaymentOutboxWorker(DapperContext context)
        {
            _context = context;
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

            Console.WriteLine("📤 PaymentOutboxWorker çalışıyor...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessOutboxMessages(channel, stoppingToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ PaymentOutboxWorker error: {ex.Message}");
                }

                // CPU yakmamak için bekleme
                await Task.Delay(3000, stoppingToken);
            }
        }

        private async Task ProcessOutboxMessages(IChannel channel, CancellationToken stoppingToken)
        {
            const string sql =
                @"SELECT * FROM OutboxMessages 
                  WHERE Status = 'Pending' 
                  ORDER BY CreatedAt 
                  LIMIT 10";

            using var conn = _context.CreateConnection();
            var messages = await conn.QueryAsync<OutboxMessage>(sql);

            foreach (var msg in messages)
            {
                try
                {
                    // Outbox'taki JSON Payload doğrudan publish ediliyor
                    var body = Encoding.UTF8.GetBytes(msg.Payload);

                    await channel.BasicPublishAsync(
                        exchange: "",
                        routingKey: "payment_events",
                        mandatory: false,
                        basicProperties: new BasicProperties(),
                        body: body,
                        cancellationToken: stoppingToken
                    );

                    // Mesaj başarılı → DB güncelle
                    const string update =
                        @"UPDATE OutboxMessages
                          SET Status = 'Processed', ProcessedAt = NOW()
                          WHERE Id = @Id";

                    await conn.ExecuteAsync(update, new { msg.Id });

                    Console.WriteLine($"📨 Outbox → Payment event yayınlandı | Id={msg.Id}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Outbox publish hata (Id={msg.Id}): {ex.Message}");
                    // Not: Retry mekanizması burada olabilir (Status=Failed)
                }
            }
        }
    }
}
