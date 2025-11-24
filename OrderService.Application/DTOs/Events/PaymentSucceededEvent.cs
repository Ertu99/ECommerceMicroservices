using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrderService.Application.DTOs.Events
{
    public class PaymentSucceededEvent
    {
        public int OrderId { get; set; }
        public decimal Amount { get; set; }
    }
}
