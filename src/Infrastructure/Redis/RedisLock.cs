using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Infrastructure.Redis;
public class RedisLock : IDisposable
{
    private readonly ConnectionMultiplexer _redis;
    private readonly string _key;
    private readonly string _value;
    private readonly IDatabase _db;
    private bool _disposed;
    private readonly TimeSpan _expiry;

    public RedisLock(ConnectionMultiplexer redis, string key, TimeSpan? expiry = null)
    {
        _redis = redis;
        _key = $"lock:{key}";
        _value = Guid.NewGuid().ToString();
        _db = _redis.GetDatabase();
        _expiry = expiry ?? TimeSpan.FromMinutes(1); // Default lock timeout 1 minute
    }

    public async Task<bool> AcquireAsync(int retryCount = 3, int retryDelayMs = 200)
    {
        for (int i = 0; i < retryCount; i++)
        {
            if (await _db.StringSetAsync(_key, _value, _expiry, When.NotExists))
            {
                return true;
            }

            if (i < retryCount - 1)
            {
                await Task.Delay(retryDelayMs);
            }
        }
        return false;
    }

    public async Task<bool> ReleaseAsync()
    {
        var script = @"
            if redis.call('get', KEYS[1]) == ARGV[1] then
                return redis.call('del', KEYS[1])
            else
                return 0
            end";

        var result = await _db.ScriptEvaluateAsync(script,
            new RedisKey[] { _key },
            new RedisValue[] { _value });

        return result.ToString() == "1";
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            ReleaseAsync().Wait();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
public class DistributedLockFactory
{
    private readonly ConnectionMultiplexer _redis;

    public DistributedLockFactory(string redisConnectionString)
    {
        _redis = ConnectionMultiplexer.Connect(redisConnectionString);
    }

    public RedisLock CreateLock(string key, TimeSpan? expiry = null)
    {
        return new RedisLock(_redis, key, expiry);
    }
}
