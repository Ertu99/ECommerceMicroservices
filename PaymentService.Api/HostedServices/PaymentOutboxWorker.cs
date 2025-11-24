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

            await using var connection = await factory.CreateConnectionAsync();
            await using var channel = await connection.CreateChannelAsync();

            // Payment eventleri için kuyruk
            await channel.QueueDeclareAsync(
                queue: "payment_events",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessOutboxMessages(channel , stoppingToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"PaymentOutboxWorker error: {ex.Message}");
                }

                await Task.Delay(3000, stoppingToken);
            }
        }

        private async Task ProcessOutboxMessages(IChannel channel, CancellationToken stoppingToken)
        {
            var sql = @"SELECT * FROM OutboxMessages WHERE Status = 'Pending' LIMIT 10";

            using var conn = _context.CreateConnection();
            var messages = await conn.QueryAsync<OutboxMessage>(sql);


            foreach (var msg in messages)
            {
                var envelope = new IntegrationEventEnvelope
                {
                    EventType = msg.EventType,
                    Payload = msg.Payload
                };

                var json = JsonSerializer.Serialize(envelope);
                var body = Encoding.UTF8.GetBytes(json);

                await channel.BasicPublishAsync(
                    exchange: "",
                    routingKey: "payment_events",
                    mandatory: false,
                    basicProperties: new BasicProperties(),
                    body: body,
                    cancellationToken: stoppingToken
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

