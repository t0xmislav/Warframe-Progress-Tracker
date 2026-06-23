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
        private readonly int refillBytesPerSecond; //Bucket refill rate in bytes per second
        private readonly int bucketCapacityBytes; //Maximum tokens the bucket can hold
        private double availableBytes; //Number of bytes available to grant/download
        private long lastRefillTimestamp; //Last time the bucket was refilled
        private readonly object lockBucket = new();

        ///<param name="bytesPerSecond">
        /// The rate at which the bucket refills in bytes per second. Must be > 0.
        ///</param>
        ///<param name="bucketCapacityBytes">
        /// The maximum number of bytes the bucket can hold. If 0 or less, defaults to the refill rate.
        ///</param>
        public TokenBucketRateLimiter(int bytesPerSecond, int bucketCapacityBytes = 0)
        {
            if (bytesPerSecond <= 0) throw new ArgumentOutOfRangeException(nameof(bytesPerSecond));
            refillBytesPerSecond = bytesPerSecond;
            this.bucketCapacityBytes = bucketCapacityBytes > 0 ? bucketCapacityBytes : Math.Max(1, refillBytesPerSecond);
            availableBytes = 0;
            lastRefillTimestamp = Stopwatch.GetTimestamp();
        }
        ///<summary>
        ///Refills the bucket based on the elapsed time since the last refill. Should be called before trying to acquire tokens.
        ///</summary>
        private void Refill()
        {
            var currTime = Stopwatch.GetTimestamp();
            var secondsSinceLastRefill = (currTime - lastRefillTimestamp) / (double)Stopwatch.Frequency;
            if (secondsSinceLastRefill <= 0) return;
            availableBytes = Math.Min(bucketCapacityBytes, availableBytes + secondsSinceLastRefill * refillBytesPerSecond);
            lastRefillTimestamp = currTime;
        }
        
        ///<summary>
        ///Attempts to acquire the specified number of bytes from the bucket. If not enough bytes are available, waits until they are refilled.
        ///</summary>
        ///<param name="requestedBytes">The number of bytes to acquire.</param>
        ///<param name="cancellationToken">A token to cancel the operation.</param>
        ///<returns>The number of bytes granted, which may be less than requested if the rate limit is reached.</returns>
        public async Task<int> AcquireAsync(int requestedBytes, CancellationToken cancellationToken = default)
        {
            if (requestedBytes <= 0) return 0;
            if (refillBytesPerSecond <= 0) return requestedBytes; // No rate limit

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int grantedBytes;
                int waitMs;
                lock (lockBucket)
                {
                    Refill();
                    grantedBytes = (int)Math.Floor(availableBytes);
                    if (grantedBytes > requestedBytes)
                    {
                        grantedBytes = requestedBytes;
                    }
                    if (grantedBytes > 0)
                    {
                        availableBytes -= grantedBytes;
                        return grantedBytes;
                    }
                    var neededForMinGrant = 1.0 - availableBytes; // Need at least 1 token to proceed
                    waitMs = (int)Math.Ceiling((neededForMinGrant / refillBytesPerSecond) * 1000);
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
