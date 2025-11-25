using Newtonsoft.Json;
using PaymentService.Application.Interfaces;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PaymentService.Infrastructure.Cache
{
    public class RedisCacheService : IRedisCacheService
    {
        private readonly IDatabase _db;
        public RedisCacheService(IConnectionMultiplexer redis)
        {
            _db = redis.GetDatabase();

        }

        public async Task<T?> GetAsync<T>(string key)
        {
            var json = await _db.StringGetAsync(key);

            if (json.IsNullOrEmpty)
                return default;

            return JsonConvert.DeserializeObject<T>(json!)!;
        }

        public async Task RemoveAsync(string key)
        {
            await _db.KeyDeleteAsync(key);
        }

        public async Task SetPaymentResultAsync(string key, object value, int minutes = 30)
        {
            var json = JsonConvert.SerializeObject(value);
            await _db.StringSetAsync(
                key,
                json,
                TimeSpan.FromMinutes(minutes)
            );
        }

        // IDEMPOTENCY → SETNX + EX
        public async Task<bool> TrySetIdempotencyKeyAsync(string key, int ttlSeconds = 86400)
        {
            return await _db.StringSetAsync(
              key,
              "1",
              TimeSpan.FromSeconds(ttlSeconds),
              when: When.NotExists  // SETNX
          );
        }
    }
}
