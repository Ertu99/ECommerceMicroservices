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
        
        public OrderAppService (IOrderRepository repo, IOutboxRepository outboxRepo)
        {
            _repo = repo; 
            _outboxRepo = outboxRepo;
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

        public Task<Order?> GetByIdAsync(int id)
        {
            return _repo.GetByIdAsync(id);
        }

    }
}
