using OrderService.Api.HostedServices;
using OrderService.Application.Interfaces;
using OrderService.Application.Services;
using OrderService.Infrastructure.Cache;
using OrderService.Infrastructure.Database.Dapper;
using OrderService.Infrastructure.Database.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "localhost:6379";
});

builder.Services.AddScoped<IRedisCacheService, RedisCacheService>();


// Dapper Context
builder.Services.AddSingleton(new DapperContext(
    builder.Configuration.GetConnectionString("Postgres")));
// Repository DI
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IOutboxRepository, OutboxRepository>();
// Service DI
builder.Services.AddScoped<OrderAppService>();
builder.Services.AddHostedService<OutboxWorker>();
builder.Services.AddHostedService<PaymentEventsConsumer>();





// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();





var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
