using System.IO;
using System.Net.Http;

namespace Warframe_Progress_Tracker.Utils
{
    public static class HttpDownloaderUtil
    {
        private static readonly HttpClient httpClient = new HttpClient(new SocketsHttpHandler
            {
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            });
        public static async Task<byte[]> DownloadDataAsync(string url, 
            IProgress<double>? progress = null, int speedLimitBytesPerSecond = 0, 
            CancellationToken cancellationToken = default, Uri? referer = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            request.Headers.TryAddWithoutValidation("Accept", "image/avif,image/webp,image/apng,image/*,*/*;q=0.8");
            if (referer != null)
            {
                request.Headers.Referrer = referer;
            }
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var contentLength = response.Content.Headers.ContentLength;
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var ms = contentLength.HasValue 
                ? new MemoryStream((int)contentLength.Value)
                : new MemoryStream();

            int bufferSize = speedLimitBytesPerSecond > 0
                ? Math.Clamp(speedLimitBytesPerSecond, 4096, 81920)
                : 81920;
            var buffer = new byte[bufferSize];
            long totalRead = 0;

            TokenBucketRateLimiter? limiter = speedLimitBytesPerSecond > 0 ? 
                new TokenBucketRateLimiter(speedLimitBytesPerSecond, bucketCapacityBytes: speedLimitBytesPerSecond) 
                : null;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                int toRequest = buffer.Length;
                int allowed = limiter is null ? toRequest 
                    : await limiter.AcquireAsync(toRequest, cancellationToken);
                int read = await stream.ReadAsync(buffer, 0, allowed, cancellationToken);
                if(read == 0) break;

                await ms.WriteAsync(buffer, 0, read, cancellationToken);
                totalRead += read;

                if(contentLength.HasValue && progress != null)
                {
                    progress.Report((double)totalRead / contentLength.Value);
                }
            }
            if(progress != null && contentLength == null)
            {
                progress.Report(1.0);
            }
            return ms.ToArray();
        }
    }
}
