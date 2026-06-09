using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Warframe_Progress_Tracker.Utils
{
    public static class PlaywrightDownloadUtil
    {
        public static async Task<byte[]> DownloadDataAsync(string url, IPage page, 
            IProgress<double>? progress = null, int speedLimitBytesPerSecond = 0, 
            CancellationToken cancellationToken = default)
        {
            string callbackName = $"__reportProgress_{Guid.NewGuid():N}";
            await page.ExposeFunctionAsync(callbackName, (double p) =>
            {
                progress?.Report(p);
                return Task.CompletedTask;
            });

            // JS function: fetch the resource as stream, read chunks, call back for progress, throttle by delaying per chunk
            string script = @"
            async ({ url, speed }) => {
                const res = await fetch(url, { credentials: 'include' });
                if (!res.ok) throw new Error('fetch failed ' + res.status);
                const lenHeader = res.headers.get('Content-Length');
                const total = lenHeader ? parseInt(lenHeader, 10) : null;
                const reader = res.body.getReader();
                const chunks = [];
                let received = 0;

                const rate = speed || 0;
                const capacity = rate > 0 ? rate : 0;
                let availableBytes = capacity;
                let last = performance.now();

                const delay = ms => new Promise(r => setTimeout(r, ms));
                while (true) {
                    if(rate && rate > 0) {
                        const now = performance.now();
                        const elapsed = (now - last) / 1000;
                        availableBytes = Math.min(capacity, availableBytes + elapsed * rate);
                        last = now;
                        if(availableBytes < 1) {
                            const needed = 1 - availableBytes;
                            await delay(Math.Ceil(needed / rate) * 1000);
                            continue;
                        }
                    }
                    const { done, value } = await reader.read();
                    if (done) break;
                    chunks.push(value);
                    received += value.length;

                    try{
                        if(total != null) {
                            await window['" + callbackName + @"'](received / total);
                        } else {
                            window['" + callbackName + @"'](0);
                        }
                    } catch(e) {
                        // Ignore callback errors (e.g. if page navigated or callback removed)
                    }
                    if(rate && rate > 0) {
                        availableBytes -= value.byteLength;
                        if(availableBytes < 0) {
                            const deficit = -availableBytes;
                            await delay(Math.Ceil(deficit / rate) * 1000);
                            availableBytes = Math.max(0, availableBytes); // Avoid negative availableBytes after delay
                            last = performance.now(); // Reset last after delay to avoid token miscalculation
                        }
                    }
                }
                // concatenate into single Uint8Array
                let size = chunks.reduce((s, c) => s + c.byteLength, 0);
                let merged = new Uint8Array(size);
                let offset = 0;
                for (const c of chunks) { merged.set(c, offset); offset += c.byteLength; }
                // convert to base64
                // convert to binary string in reasonable chunks to avoid call stack issues
                const chunkSize = 0x8000;
                let b64 = '';
                for (let i = 0; i < merged.length; i += chunkSize) {
                    const sub = merged.subarray(i, i + chunkSize);
                    b64 += String.fromCharCode.apply(null, Array.from(sub));
                }
                return btoa(b64);
            }";

            try
            {
                // Evaluate in the page; pass speed as number of bytes/sec
                var result = await page.EvaluateAsync<string>(script, new { url, speed = speedLimitBytesPerSecond });
                if (string.IsNullOrEmpty(result)) return Array.Empty<byte>() as byte[] ?? new byte[0];
                // Playwright returns base64 string (btoa result). Convert to bytes.
                return Convert.FromBase64String(result);
            }
            finally
            {
            }
        }
    }
}
