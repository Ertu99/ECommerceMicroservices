using PaymentService.Application.DTOs.Events;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PaymentService.Application.Services
{
    public class PaymentAppService
    {
        private readonly IPaymentRepository _paymentRepo;
        private readonly IOutboxRepository _outboxRepo;
        public PaymentAppService(IPaymentRepository paymentRepo, IOutboxRepository outboxRepo )
        {
            _paymentRepo = paymentRepo;
            _outboxRepo = outboxRepo;
        }

        public async Task ProcessPaymentAsync(OrderCreatedEvent evt)
        {
            // %50 ihtimalle ödeme başarısız simülasyonu
            var random = new Random();
            bool isSuccess = random.Next(0, 100) >= 30;
           
            if (isSuccess)
            {
                await HandleSuccess(evt);
            }
            else
            {
                await HandleFail(evt);
            }
        }

        private async Task HandleSuccess(OrderCreatedEvent evt)
        {
            var payment = new Payment
            {
                OrderId = evt.OrderId,
                Amount = evt.TotalAmount,
                Status = "Succeeded",
                CreatedAt = DateTime.UtcNow
            };

            await _paymentRepo.AddAsync( payment );

            var successEvent = new PaymentSucceededEvent
            {
                OrderId = evt.OrderId,
                Amount = evt.TotalAmount
            };

            var json = JsonSerializer.Serialize( successEvent );

            var outbox = new OutboxMessage
            {
                EventType = "PaymentSucceeded",
                Payload = json,
                CreatedAt = DateTime.UtcNow
            }; ;
            await _outboxRepo.AddAsync( outbox );
        }

        private async Task HandleFail(OrderCreatedEvent evt)
        {
            var payment = new Payment
            {
                OrderId = evt.OrderId,
                Amount = evt.TotalAmount,
                Status = "Failed",
                CreatedAt = DateTime.UtcNow
            };

            await _paymentRepo.AddAsync ( payment );

            var failEvent = new PaymentFailedEvent
            {
                OrderId = evt.OrderId,
                Reason = "Insufficient balance"
            };

            var json = JsonSerializer.Serialize(failEvent);

            var outbox = new OutboxMessage
            {
                EventType = "PaymentFailed",
                Payload = json,
                CreatedAt = DateTime.UtcNow
            };

            await _outboxRepo.AddAsync(outbox);
        }

    }
}
