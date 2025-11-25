using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PaymentService.Application.Interfaces
{
    public interface IRedisCacheService
    {
        // ============================
        // 1) Idempotency (SETNX + EX)
        // ============================
        Task<bool> TrySetIdempotencyKeyAsync(string key, int ttlSeconds = 86400);

        // ============================
        // 2) Payment Result Cache
        // ============================

        // SET → Payment result
        Task SetPaymentResultAsync(string key, object value, int minutes = 30);

        // GET → Payment result (generic)
        Task<T?> GetAsync<T>(string key);

        // REMOVE → Cache invalidate
        Task RemoveAsync(string key);
    }
}

