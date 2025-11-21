using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using PaymentService.Application.Services;
using PaymentService.Application.DTOs.Events;

namespace PaymentService.Api.HostedServices
{
    public class OrderCreatedConsumer : BackgroundService
    {
        private readonly PaymentAppService _paymentService;

        public OrderCreatedConsumer(PaymentAppService paymentService)
        {
            _paymentService = paymentService;
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

            await channel.QueueDeclareAsync(
                queue: "order_events",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += async (sender, ea) =>
            {
                var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                var evt = JsonSerializer.Deserialize<OrderCreatedEvent>(json);

                if (evt != null)
                {
                    await _paymentService.ProcessPaymentAsync(evt);
                }

                await channel.BasicAckAsync(ea.DeliveryTag, false);
            };

            await channel.BasicConsumeAsync(
                queue: "order_events",
                autoAck: false,
                consumer: consumer
            );

            Console.WriteLine("💳 PaymentService OrderCreated eventlerini dinliyor...");
        }
    }
}
