using Microsoft.Extensions.Logging;
using PMPlatform.Application.Common.RateLimiting;
using StackExchange.Redis;

namespace PMPlatform.Infrastructure.RateLimiting;

/// <summary>
/// Counters shared by every API instance, in the Redis-compatible store <c>RATE_LIMIT_STORE_CONNECTION_STRING</c> names
/// (TASK-078). One script increments a key and, on its first request, sets the key to expire when the window closes, so a
/// count and its expiry never drift apart between instances. When the store cannot be reached the command fails at once
/// rather than queueing, and the request is let through (throttling degrades rather than fails).
/// </summary>
internal sealed partial class RedisRateLimitStore : IRateLimitStore, IDisposable
{
    public const string KeyPrefix = "pmplatform:rate-limit:";

    // Returns the count and the window's remaining milliseconds. A key left without an expiry (it cannot happen through this
    // script, but a manual write could) is given one rather than counting forever.
    private const string IncrementScript = """
        local count = redis.call('INCR', KEYS[1])
        if count == 1 then
          redis.call('PEXPIRE', KEYS[1], ARGV[1])
        end
        local remaining = redis.call('PTTL', KEYS[1])
        if remaining < 0 then
          redis.call('PEXPIRE', KEYS[1], ARGV[1])
          remaining = tonumber(ARGV[1])
        end
        return { count, remaining }
        """;

    private readonly Lazy<ConnectionMultiplexer> _connection;
    private readonly ILogger<RedisRateLimitStore> _logger;

    public RedisRateLimitStore(string connectionString, ILogger<RedisRateLimitStore> logger)
    {
        ConfigurationOptions options = ConfigurationOptions.Parse(connectionString);
        options.AbortOnConnectFail = false;
        options.BacklogPolicy = BacklogPolicy.FailFast;
        _connection = new Lazy<ConnectionMultiplexer>(() => ConnectionMultiplexer.Connect(options));
        _logger = logger;
    }

    public async Task<RateLimitCount?> IncrementAsync(string key, TimeSpan window, CancellationToken cancellationToken)
    {
        try
        {
            RedisResult[] result = (RedisResult[])(await _connection.Value.GetDatabase()
                .ScriptEvaluateAsync(IncrementScript, [new RedisKey(KeyPrefix + key)], [(long)window.TotalMilliseconds])
                .ConfigureAwait(false))!;
            return new RateLimitCount((long)result[0], TimeSpan.FromMilliseconds((long)result[1]));
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            // The type only: the client's message can name the store's endpoint (CTL-27).
            LogUnavailable(_logger, exception.GetType().Name);
            return null;
        }
    }

    public void Dispose()
    {
        if (_connection.IsValueCreated)
        {
            _connection.Value.Dispose();
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The rate-limit store could not count a request ({Reason}); the request is let through.")]
    private static partial void LogUnavailable(ILogger logger, string reason);
}
