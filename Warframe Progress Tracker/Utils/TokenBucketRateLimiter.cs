using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Warframe_Progress_Tracker.Utils
{
    public sealed class TokenBucketRateLimiter
    {
        private readonly int refillBytesPerSecond;
        private readonly int capacityBytes;
        private double tokens;
        private long lastTimestamp;
        private readonly object lockObj = new();

        public TokenBucketRateLimiter(int bytesPerSecond, int capacityBytes = 0)
        {
            if (bytesPerSecond <= 0) throw new ArgumentOutOfRangeException(nameof(bytesPerSecond));
            refillBytesPerSecond = bytesPerSecond;
            this.capacityBytes = capacityBytes > 0 ? capacityBytes : Math.Max(1, refillBytesPerSecond);
            tokens = this.capacityBytes;
            lastTimestamp = Stopwatch.GetTimestamp();
        }

        private void Refill()
        {
            var now = Stopwatch.GetTimestamp();
            var elapsedSeconds = (now - lastTimestamp) / (double)Stopwatch.Frequency;
            if (elapsedSeconds <= 0) return;
            tokens = Math.Min(capacityBytes, tokens + elapsedSeconds * refillBytesPerSecond);
            lastTimestamp = now;
        }

        public async Task<int> AcquireAsync(int maxBytes, CancellationToken cancellationToken = default)
        {
            if (maxBytes <= 0) return 0;
            if (refillBytesPerSecond <= 0) return maxBytes; // No rate limit

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int allowed;
                int waitMs;
                lock (lockObj)
                {
                    Refill();
                    allowed = (int)Math.Floor(tokens);
                    if (allowed > maxBytes)
                    {
                        allowed = maxBytes;
                    }
                    if (allowed > 0)
                    {
                        tokens -= allowed;
                        return allowed;
                    }
                    var needed = 1.0 - tokens; // Need at least 1 token to proceed
                    waitMs = (int)Math.Ceiling((needed / refillBytesPerSecond) * 1000);
                    if (waitMs < 1)
                    {
                        waitMs = 1; // Minimum wait to avoid busy waiting
                    }
                }
                // Wait a bit before retrying to avoid busy waiting
                await Task.Delay(waitMs, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
