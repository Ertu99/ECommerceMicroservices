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

            // 1. Bağlantı ve Kanalı ASYNC olarak oluşturuyoruz.
            // "await using" sayesinde servis durduğunda bağlantılar otomatik ve düzgün kapanır.
            await using var connection = await factory.CreateConnectionAsync();
            await using var channel = await connection.CreateChannelAsync();

            // 2. Kuyruk tanımlama da artık async
            await channel.QueueDeclareAsync(
                queue: "order_events",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Kanalı parametre olarak gönderiyoruz
                    await ProcessOutboxMessages(channel);
                }
                catch (Exception ex)
                {
                     
                }

                await Task.Delay(3000, stoppingToken);
            }
        }

        private async Task ProcessOutboxMessages(IChannel channel)
        {
            var sql = @"SELECT * FROM OutboxMessages WHERE Status = 'Pending' LIMIT 10";

            using var conn = _context.CreateConnection();
            var messages = await conn.QueryAsync<OutboxMessage>(sql);

            foreach (var msg in messages)
            {
                var body = Encoding.UTF8.GetBytes(msg.Payload);

                await channel.BasicPublishAsync(
                    exchange: "",
                    routingKey: "order_events",
                    mandatory: false,
                    basicProperties: new BasicProperties(), // veya null yerine yeni instance
                    body: body
                );

                var updateSql = @"
                    UPDATE OutboxMessages 
                    SET Status = 'Processed', ProcessedAt = NOW()
                    WHERE Id = @Id";

                await conn.ExecuteAsync(updateSql, new { msg.Id });
            }
        }
    }
}