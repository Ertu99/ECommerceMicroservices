using PaymentService.Api.HostedServices;
using PaymentService.Application.Interfaces;
using PaymentService.Application.Services;
using PaymentService.Infrastructure.Cache;
using PaymentService.Infrastructure.Database.Dapper;
using PaymentService.Infrastructure.Database.Repositories;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// ==========================
// Services
// ==========================

builder.Services.AddControllers();

// Redis Connection
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect("localhost:6379"));

// Redis Cache Service
builder.Services.AddScoped<IRedisCacheService, RedisCacheService>();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Dapper Context
builder.Services.AddSingleton(new DapperContext(
    builder.Configuration.GetConnectionString("Postgres")
));

// Application Services
builder.Services.AddScoped<PaymentAppService>();

// Repositories
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IOutboxRepository, OutboxRepository>();

// Background Workers
builder.Services.AddHostedService<OrderCreatedConsumer>();
builder.Services.AddHostedService<PaymentOutboxWorker>();

// ==========================
// Build App
// ==========================

var app = builder.Build();

// Middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
