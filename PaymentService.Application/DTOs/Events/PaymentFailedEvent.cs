using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PaymentService.Application.DTOs.Events
{
    public class PaymentFailedEvent
    {
        public int OrderId { get; set; }
        public string Reason { get; set; } = "";
    }
}
