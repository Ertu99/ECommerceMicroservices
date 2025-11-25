using Microsoft.Extensions.Caching.Distributed;
using OrderService.Application.Interfaces;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace OrderService.Infrastructure.Cache
{
    public class RedisCacheService : IRedisCacheService
    {
        private readonly IDistributedCache _cache;

        public RedisCacheService(IDistributedCache cache)
        {
            _cache = cache;
        }

        // =======================
        // GET
        // =======================
        public async Task<T?> GetAsync<T>(string key)
        {
            var json = await _cache.GetStringAsync(key);
            return json == null ? default : JsonSerializer.Deserialize<T>(json);
        }

        // =======================
        // REMOVE
        // =======================
        public async Task RemoveAsync(string key)
        {
            await _cache.RemoveAsync(key);
        }

        // =======================
        // SET (Absolute TTL)
        // =======================
        public async Task SetAbsoluteAsync<T>(string key, T value, int minutes)
        {
            var json = JsonSerializer.Serialize(value);

            await _cache.SetStringAsync(
                key,
                json,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(minutes)
                });
        }
    }
}
