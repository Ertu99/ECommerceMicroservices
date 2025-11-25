using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrderService.Application.Redis
{
    public static class CacheKeys
    {
        public static string OrderDetails(int id)
            => $"order:details:{id}";

        public static string OrderIdempotency(string eventId)
            => $"order:idempotency:{eventId}";
    }
}
