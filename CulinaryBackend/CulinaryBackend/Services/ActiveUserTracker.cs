using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;

namespace CulinaryBackend.Services
{
    public class ActiveUserTracker
    {
        private readonly IMemoryCache _cache;
        private readonly ConcurrentDictionary<string, bool> _activeKeys = new();

        public ActiveUserTracker(IMemoryCache cache)
        {
            _cache = cache;
        }

        public void Ping(string deviceId, double lat, double lng)
        {
            _activeKeys.TryAdd(deviceId, true);

            var options = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromSeconds(20))
                .RegisterPostEvictionCallback((key, value, reason, state) =>
                {
                    if (reason != EvictionReason.Replaced)
                    {
                        _activeKeys.TryRemove(key.ToString()!, out _);
                    }
                });

            _cache.Set(deviceId, new { Latitude = lat, Longitude = lng }, options);
        }

        public int GetActiveUserCount()
        {
            return _activeKeys.Count;
        }
    }
}