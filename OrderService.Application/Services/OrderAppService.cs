using OrderService.Application.DTOs;
using OrderService.Application.DTOs.Events;
using OrderService.Application.Interfaces;
using OrderService.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace OrderService.Application.Services
{
    public class OrderAppService
    {
        private readonly IOrderRepository _repo;
        private readonly IOutboxRepository _outboxRepo;
        private readonly IRedisCacheService _cache;

        public OrderAppService(IOrderRepository repo, IOutboxRepository outboxRepo, IRedisCacheService cache)
        {
            _repo = repo;
            _outboxRepo = outboxRepo;
            _cache = cache;
        }

        public async Task<int> CreateOrderAsync(CreateOrderDto dto)
        {
            if (dto.TotalAmount <= 0)
                throw new Exception("Order amount must be greater than zero");

            var order = new Order
            {
                CustomerName = dto.CustomerName,
                TotalAmount = dto.TotalAmount,
                Status = "Created",
                CreatedAt = DateTime.UtcNow
            };

            var orderId = await _repo.CreateAsync(order);
            // 1) Event nesnesini oluştur
            var orderCreatedEvent = new OrderCreatedEvent
            {
                OrderId = orderId,
                CustomerName = order.CustomerName,
                TotalAmount = order.TotalAmount
            };

            // 2) JSON’a çevir
            var payload = JsonSerializer.Serialize(orderCreatedEvent);

            // 3) Outbox kaydı oluştur
            var outbox = new OutboxMessage
            {
                EventType = "OrderCreated",
                Payload = payload,
                CreatedAt = DateTime.UtcNow,
                Status = "Pending"
            };

            // 4) DB’ye kaydet
            await _outboxRepo.AddAsync(outbox);

            return orderId;

        }

        public async Task<OrderDto?> GetByIdAsync(int id)
        {
            var cacheKey = $"order:{id}";

            // 1) Önce cache’de var mı?
            var cached = await _cache.GetAsync<OrderDto>(cacheKey);
            if (cached != null)
                return cached;

            // 2) DB’den getir
            var order = await _repo.GetByIdAsync(id);
            if (order == null)
                return null;
            // 3) Mapping: Entity → DTO

            var dto = new OrderDto
            {
                Id = order.Id,
                CustomerName = order.CustomerName,
                TotalAmount = order.TotalAmount,
                Status = order.Status

            };
            // 4) Cache'e yaz
            await _cache.SetAsync(cacheKey,dto,minutes : 2);
            return dto;
        }

        public async Task HandlePaymentSucceededAsync(PaymentSucceededEvent evt)
        {
            // SAGA: Order -> Paid
            await _repo.UpdateStatusAsync(evt.OrderId, "Paid");
            Console.WriteLine($"✅ Order {evt.OrderId} ödeme başarılı → Status = Paid");
        }

        public async Task HandlePaymentFailedAsync(PaymentFailedEvent evt)
        {
            // SAGA Compensation: Order -> Cancelled
            await _repo.UpdateStatusAsync(evt.OrderId, "Cancelled");
            Console.WriteLine($"❌ Order {evt.OrderId} ödeme başarısız → Status = Cancelled (Reason: {evt.Reason})");

        }
    }
}
