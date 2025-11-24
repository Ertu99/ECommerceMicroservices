using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrderService.Application.Interfaces
{
    public interface IRedisCacheService
    {
        Task SetAsync<T>(string key, T value, int minutes = 5);
        Task<T?> GetAsync<T>(string key);
        Task RemoveAsync(string key);
    }
}
