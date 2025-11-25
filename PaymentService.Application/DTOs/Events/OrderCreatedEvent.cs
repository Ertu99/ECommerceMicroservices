using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PaymentService.Application.DTOs.Events
{
    public class OrderCreatedEvent
    {
        public int OrderId { get; set; }
        public string CustomerName { get; set; } = "";
        public decimal TotalAmount { get; set; }
        public Guid EventId { get; set; } = Guid.NewGuid();
    }
}
