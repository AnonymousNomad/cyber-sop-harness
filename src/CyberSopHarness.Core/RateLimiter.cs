namespace CyberSopHarness.Core;

public sealed record RateLease(string LeaseId, string Target);

public sealed class RateLimiter
{
    private sealed class TokenBucket
    {
        public TokenBucket(double rate)
        {
            Rate = rate;
            Capacity = Math.Max(1d, rate);
            Tokens = Capacity;
        }

        public double Rate { get; }
        public double Capacity { get; }
        public double Tokens { get; private set; }
        public DateTimeOffset LastRefill { get; private set; } = DateTimeOffset.MinValue;

        public void Refill(DateTimeOffset now)
        {
            if (LastRefill == DateTimeOffset.MinValue)
            {
                LastRefill = now;
                return;
            }
            var elapsed = (now - LastRefill).TotalSeconds;
            if (elapsed <= 0) return;
            Tokens = Math.Min(Capacity, Tokens + elapsed * Rate);
            LastRefill = now;
        }

        public bool CanTake(DateTimeOffset now)
        {
            Refill(now);
            return Tokens >= 1d;
        }

        public void Take() => Tokens -= 1d;
    }

    private readonly RateLimitDefinition _limits;
    private readonly TokenBucket _global;
    private readonly Dictionary<string, TokenBucket> _perTarget = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RateLease> _leases = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private int _activeGlobal;

    public RateLimiter(RateLimitDefinition limits)
    {
        if (!double.IsFinite(limits.RequestsPerSecond) || limits.RequestsPerSecond <= 0 || limits.Concurrency <= 0 || limits.PayloadBytes <= 0) throw new ArgumentException("rate limits must be finite and positive", nameof(limits));
        _limits = limits;
        _global = new TokenBucket(limits.RequestsPerSecond);
    }

    public bool TryAcquire(string target, long payloadBytes, out RateLease? lease)
    {
        var now = AuthoritativeClock.UtcNow;
        lock (_gate)
        {
            lease = null;
            if (payloadBytes < 0 || payloadBytes > _limits.PayloadBytes) return false;
            if (!_perTarget.TryGetValue(target, out var targetBucket)) _perTarget[target] = targetBucket = new TokenBucket(_limits.RequestsPerSecond);
            var activeTarget = _leases.Values.Count(item => string.Equals(item.Target, target, StringComparison.OrdinalIgnoreCase));
            if (_activeGlobal >= _limits.Concurrency || activeTarget >= _limits.Concurrency || !_global.CanTake(now) || !targetBucket.CanTake(now)) return false;
            _global.Take();
            targetBucket.Take();
            _activeGlobal++;
            lease = new RateLease("lease_" + Guid.NewGuid().ToString("N"), target);
            _leases.Add(lease.LeaseId, lease);
            return true;
        }
    }

    public bool Release(RateLease lease)
    {
        lock (_gate)
        {
            if (!_leases.Remove(lease.LeaseId)) return false;
            if (_activeGlobal > 0) _activeGlobal--;
            return true;
        }
    }
}
