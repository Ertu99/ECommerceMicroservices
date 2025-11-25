using System.Threading.Tasks;

namespace OrderService.Application.Interfaces
{
    public interface IRedisCacheService
    {
        /// <summary>
        /// Veriyi cache'e yazar (Absolute Expiration).
        /// </summary>
        /// <typeparam name="T">Kaydedilecek tip</typeparam>
        /// <param name="key">Redis key</param>
        /// <param name="value">Kaydedilecek veri</param>
        /// <param name="minutes">Kaç dakika cache'te kalacağı</param>
        Task SetAbsoluteAsync<T>(string key, T value, int minutes);

        /// <summary>
        /// Veriyi cache'ten okur. Bulamazsa default(T) döner.
        /// </summary>
        Task<T?> GetAsync<T>(string key);

        /// <summary>
        /// Verilen key'i cache'ten siler.
        /// </summary>
        Task RemoveAsync(string key);
    }
}
