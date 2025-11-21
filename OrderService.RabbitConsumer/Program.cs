using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

// RabbitMQ connection factory
var factory = new ConnectionFactory
{
    HostName = "localhost",
    UserName = "guest",
    Password = "guest"
};

// ASYNC connection + channel
await using var connection = await factory.CreateConnectionAsync();
await using var channel = await connection.CreateChannelAsync();

// Kuyruğu dinlemeden önce declare ederek garanti altına alıyoruz
await channel.QueueDeclareAsync(
    queue: "order_events",
    durable: true,
    exclusive: false,
    autoDelete: false,
    arguments: null
);

Console.WriteLine("📡 RabbitMQ Consumer hazır. Mesaj bekleniyor...");

// Consumer oluştur (ASYNC)
var consumer = new AsyncEventingBasicConsumer(channel);

// Event geldiğinde tetiklenir
consumer.ReceivedAsync += async (sender, ea) =>
{
    var body = ea.Body.ToArray();
    var message = Encoding.UTF8.GetString(body);

    Console.WriteLine($"📩 Event alındı: {message}");

    // basic ack async
    await channel.BasicAckAsync(
        deliveryTag: ea.DeliveryTag,
        multiple: false
    );
};

// Consume başlat
await channel.BasicConsumeAsync(
    queue: "order_events",
    autoAck: false,
    consumer: consumer
);

Console.WriteLine("Dinleme başlatıldı...");
Console.ReadLine();

