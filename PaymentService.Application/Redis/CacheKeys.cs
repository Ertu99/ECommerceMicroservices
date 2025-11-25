using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PaymentService.Application.Redis
{
    public class CacheKeys
    {
        public static string PaymentIdempotency(string eventId)
            => $"payment:idempotency:{eventId}";
        public static string PaymentResult(int orderId)
            => $"payment:result:{orderId}";
    }
}
