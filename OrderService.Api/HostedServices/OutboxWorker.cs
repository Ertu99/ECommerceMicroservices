using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using Dapper;
using OrderService.Infrastructure.Database.Dapper;
using OrderService.Domain.Entities;

namespace OrderService.Api.HostedServices
{
    public class OutboxWorker : BackgroundService
    {
        private readonly DapperContext _context;

        public OutboxWorker(DapperContext context)
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

            // Bağlantı ve kanal
            await using var connection = await factory.CreateConnectionAsync(stoppingToken);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

            // Sadece EXCHANGE tanımlıyoruz (publisher tarafı)
            await channel.ExchangeDeclareAsync(
                exchange: "order_exchange",
                type: "direct",
                durable: true,
                autoDelete: false,
                arguments: null,
                cancellationToken: stoppingToken
            );

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessOutboxMessages(channel, stoppingToken);
                }
                catch (Exception ex)
                {
                    // TODO: Serilog ekleyince buraya log yazacağız
                    Console.WriteLine($"[OutboxWorker] Hata: {ex.Message}");
                }

                await Task.Delay(3000, stoppingToken);
            }
        }

        private async Task ProcessOutboxMessages(IChannel channel, CancellationToken cancellationToken)
        {
            const string sql = @"SELECT * FROM OutboxMessages WHERE Status= 'Pending' LIMIT 10";

            using var conn = _context.CreateConnection();
            var messages = await conn.QueryAsync<OutboxMessage>(sql);

            foreach (var msg in messages)
            {
                var body = Encoding.UTF8.GetBytes(msg.Payload);

                // Artık DIRECT EXCHANGE'e publish ediyoruz
                await channel.BasicPublishAsync(
                    exchange: "order_exchange",
                    routingKey: "order.created",
                    mandatory: false,
                    basicProperties: new BasicProperties(), 
                    body: body,
                    cancellationToken: cancellationToken
                );

                const string updateSql = @"
                    UPDATE OutboxMessages
                    SET Status = 'Processed', ProcessedAt = NOW()
                    WHERE Id = @Id";

                await conn.ExecuteAsync(updateSql, new { msg.Id });
            }
        }
    }
}
