using Newtonsoft.Json.Linq;
using SQLitePCL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows;
using Warframe_Progress_Tracker.Utils;
using Warframe_Progress_Tracker.Utils.Logger;

namespace Warframe_Progress_Tracker.Services
{
    internal class ApiService
    {
        private static readonly HttpClient httpClient = new HttpClient();

        private static readonly int BatchSize = 50;
        private static readonly object batchLock = new object();
        public static async Task<List<Model.Item>> FetchItemsAsync(
            IProgress<(string key, object[] args)>? progress, 
            CancellationToken cancellationToken = default, 
            HashSet<string>? existingUniqueNames = null)
        {
            var sw = Stopwatch.StartNew();
            progress?.Report(("LoadingFetchingItemsStr", new object[] { }));

            var response = await httpClient.GetStringAsync("https://api.warframestat.us/items");
            var jsonArray = JArray.Parse(response);
            var items = new List<Model.Item>();
            int count = 0;

            foreach (var item in jsonArray)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var uniqueName = (string)item["uniqueName"];
                /*if (existingUniqueNames.Contains(uniqueName)) 
                {
                    Debug.WriteLine($"Item Exists {uniqueName}");
                    continue;
                }*/
                Debug.WriteLine("populating...");
                var masteryReq = item["masteryReq"]?.ToObject<int?>() ?? -1;
                var category = (string)item["category"];
                var excludeFromCodex = item["excludeFromCodex"]?.ToObject<bool>() ?? false;


                //Skip all items not granting mastery, including mission nodes
                bool grantsMastery;
                if(PluginLoader.GrantsMastery != null)
                {
                    grantsMastery = PluginLoader.GrantsMastery(uniqueName, category, masteryReq, excludeFromCodex);
                }
                else
                {
                    Debug.WriteLine("Grants Not Found defaulting to false");
                    grantsMastery = false;
                }
                if (!grantsMastery) continue;
                count++;
                if(count % 10 == 0)
                {
                    progress?.Report(("ProcessedItemsStr", new object[] { count }));
                }
                int masteryPoints;
                if(PluginLoader.GetMasteryPoints != null)
                {
                    masteryPoints = PluginLoader.GetMasteryPoints(category, uniqueName);
                }
                else
                {
                    Debug.WriteLine("Mastery Assigner not found, defaulting mastery points to 0");
                    masteryPoints = 0;
                }

                var imageName = (string)item["imageName"];
                byte[] imageBytes = null;

                if (!string.IsNullOrEmpty(imageName))
                {
                    var imageUrl = "https://cdn.warframestat.us/img/" + imageName;
                    try
                    {
                        imageBytes = await DownloadImageAsync(imageUrl, progress, (String)item["name"],
                            cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Downloading image failed for {imageUrl} with error {ex.Message}");
                    }
                }
                var parsed = new Model.Item
                {
                    Name = (String)item["name"],
                    UniqueName = (String)item["uniqueName"],
                    Category = new Model.Category {DisplayName = category},
                    MasteryPoints = masteryPoints,
                    Image = imageBytes
                };
                items.Add(parsed);
                if(items.Count >= BatchSize)
                {
                    try
                    {
                        DbService.SaveItemsBatch(items);
                        items.Clear();
                        progress.Report(("SavingItemsStr", new object[] { }));
                    }
                    catch
                    {
                        LoggerService.Log("DbSaveFailed", $"Saving items batch failed, retrying individually");
                    }
                }
                
            }
            if (items.Count > 0)
            {
                try
                {
                    DbService.SaveItemsBatch(items);
                    items.Clear();
                    progress.Report(("SavingItemsStr", new object[] { }));
                }
                catch
                {
                    LoggerService.Log("DbSaveFailed", $"Saving items batch failed, retrying individually");
                }
            }
            sw.Stop();
            LoggerService.Log("Api Performance", $"Single thread time: {sw.ElapsedMilliseconds}ms | Items processed: {count}");
            progress?.Report(("FinishedItemFetchStr", new object[] {}));
            return items;
        }

        public static async Task<bool> FetchWarframeProfile(string displayName, int id)
        {
            string wfStatApi = $"https://api.warframestat.us/profile/{Uri.EscapeDataString(displayName)}";
            //Try getting from WarframeStat
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, wfStatApi);
                using var response = await httpClient.SendAsync(req, HttpCompletionOption.ResponseContentRead);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    Debug.WriteLine($"Profile not found at {wfStatApi}");
                    LoggerService.Log("ApiFallback", $"Profile not found at {wfStatApi}");
                }

                response.EnsureSuccessStatusCode();

                var text = await response.Content.ReadAsStringAsync();
                var arr = JArray.Parse(text);

                foreach(var profile in arr)
                {
                    if(string.Equals((string)profile["displayName"], displayName, StringComparison.OrdinalIgnoreCase))
                    {
                        DbService.SetUserWfAccount(id, (string)profile["displayName"], (string)profile["platform"], (string)profile["accountId"]);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.Log("ApiFallback", $"Request to {wfStatApi} failed with error {ex.Message}");
                Debug.WriteLine($"Request to {wfStatApi} failed with error {ex.Message}");
            }

            string wfMarketApi = $"https://api.warframe.market/v2/user/{Uri.EscapeDataString(displayName.ToLower())}";
            //Try getting from Warframe Market Api
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, wfMarketApi);
                using var response = await httpClient.SendAsync(req, HttpCompletionOption.ResponseContentRead);
                Debug.WriteLine("Response received from Warframe Market API");
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Debug.WriteLine($"Profile not found at {wfMarketApi}");
                    LoggerService.Log("ApiFallback", $"Profile not found at {wfMarketApi}");
                }

                response.EnsureSuccessStatusCode();
                Debug.WriteLine($"Profile found at {wfMarketApi}, parsing response");
                var text = await response.Content.ReadAsStringAsync();
                var profile = JObject.Parse(text);
                Debug.WriteLine($"Profile found at {wfMarketApi}, parsing response");

                Debug.WriteLine($"Profile found at {wfMarketApi} with ingame name {(string)profile["data"]["ingameName"]}");
                DbService.SetUserWfAccount(id, (string)profile["data"]["ingameName"], (string)profile["data"]["id"], (string)profile["data"]["platform"]);
                return true;
                
            }
            catch (Exception ex)
            {
                LoggerService.Log("ApiFallback", $"Request to {wfMarketApi} failed with error {ex.Message}");
                Debug.WriteLine($"Request to {wfMarketApi} failed with error {ex.Message}");
            }


            return false;
        }
        public static async Task<byte[]> DownloadImageAsync(string url, IProgress<(string key, object[] args)>? progress, string name, CancellationToken cancellationToken = default)
        {
            int speedLimitKB = 0;
            try
            {
                int.TryParse(IniFileService.Read("Download", "SpeedLimit", "0"), out speedLimitKB);

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Reading Speed limit settings failed {ex.Message}");
            }
            int speedLimitBtytes = Math.Max(0, speedLimitKB) * 1024;
            IProgress<double>? imgProgress = new Progress<double>(p =>
            {
                progress.Report(("DownloadingImageProgressStr", new object[] { name, (int)(p * 100) }));
            });

            try
            {
                return await HttpDownloaderUtil.DownloadDataAsync
                    (url,
                    imgProgress,
                    speedLimitBtytes, cancellationToken);
            }
            catch
            {
                using var client = new HttpClient();
                return await client.GetByteArrayAsync(url);
            }
        }
        public static async Task<List<Model.Item>> FetchItemsAsyncMultithread(
            IProgress<(string key, object[] args)>? progress,
            CancellationToken cancellationToken = default,
            HashSet<string>? existingUniqueNames = null)
        {
            var sw = Stopwatch.StartNew();
            progress?.Report(("LoadingFetchingItemsStr", new object[] { }));

            var response = await httpClient.GetStringAsync("https://api.warframestat.us/items");
            var jsonArray = JArray.Parse(response);

            var itemsToProcess = new List<(JToken token, string name, string? imageUrl, int masteryPoints, string category)>();

            foreach (var item in jsonArray)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var uniqueName = (string)item["uniqueName"];
                //if (existingUniqueNames?.Contains(uniqueName) == true) continue;

                var masteryReq = item["masteryReq"]?.ToObject<int?>() ?? -1;
                var category = (string)item["category"];
                var excludeFromCodex = item["excludeFromCodex"]?.ToObject<bool>() ?? false;

                bool grantsMastery = PluginLoader.GrantsMastery != null
                    ? PluginLoader.GrantsMastery(uniqueName, category, masteryReq, excludeFromCodex)
                    : false;

                if (!grantsMastery) continue;

                int masteryPoints = PluginLoader.GetMasteryPoints != null
                    ? PluginLoader.GetMasteryPoints(category, uniqueName)
                    : 0;

                var imageName = (string?)item["imageName"];
                var imageUrl = !string.IsNullOrEmpty(imageName)
                    ? "https://cdn.warframestat.us/img/" + imageName
                    : null;

                itemsToProcess.Add((item, (string)item["name"], imageUrl, masteryPoints, category));
            }

            // Get total download size via HEAD requests for percentage tracking
            progress?.Report(("LoadingHeadRequestStr", new object[] { }));
            long totalBytes = 0;
            var semaphore = new SemaphoreSlim(4);
            //var sw = Stopwatch.StartNew();
            var headTasks = itemsToProcess
                .Where(i => i.imageUrl != null)
                .Select(async i =>
                {
                    await semaphore.WaitAsync(cancellationToken);
                    try
                    {
                        using var req = new HttpRequestMessage(HttpMethod.Head, i.imageUrl);
                        using var resp = await httpClient.SendAsync(req, cancellationToken);
                        return resp.Content.Headers.ContentLength ?? 0;
                    }
                    catch { return 0L; }
                    finally { semaphore.Release(); }
                });

            var sizes = await Task.WhenAll(headTasks);
            totalBytes = sizes.Sum();


            int.TryParse(IniFileService.Read("Download", "SpeedLimit", "0"), out int speedLimitKB);
            int speedLimitBytes = Math.Max(0, speedLimitKB) * 1024;
            HttpDownloaderUtil.SetSpeedLimit(speedLimitBytes);


            // Parallel image downloads with progress
            long downloadedBytes = 0;
            var itemDownloadedBytes = new long[itemsToProcess.Count];
            var batch = new List<Model.Item>();
            var downloadTasks = itemsToProcess
                .Where(i => i.imageUrl != null)
                .Select(async i =>
                {
                    await semaphore.WaitAsync(cancellationToken);
                    try
                    {
                        
                        var bytes = await HttpDownloaderUtil.DownloadDataAsync(
                            i.imageUrl!,
                            new Progress<double>(p =>
                            {
                                int idx = itemsToProcess.IndexOf(i);
                                long itemTotal = sizes[idx];
                                long newItemBytes = (long)(p * itemTotal);
                                long delta = newItemBytes - Interlocked.Read(ref itemDownloadedBytes[idx]);

                                Interlocked.Exchange(ref itemDownloadedBytes[idx], newItemBytes);
                                var total = Interlocked.Add(ref downloadedBytes, delta);

                                var percentage = totalBytes > 0 ? (int)((double)total / totalBytes * 100) : 0;
                                progress?.Report(("DownloadingImageProgressStr", new object[] { i.name, percentage }));
                            }),
                            cancellationToken: cancellationToken);

                        var item = new Model.Item
                        {
                            Name = i.name,
                            UniqueName = (string)i.token["uniqueName"],
                            Category = new Model.Category { DisplayName = i.category },
                            MasteryPoints = i.masteryPoints,
                            Image = bytes
                        };
                        bytes = null;
                        lock (batchLock)
                        {
                            batch.Add(item);
                            if (batch.Count >= BatchSize)
                            {
                                DbService.SaveItemsBatch(batch.ToList());
                                batch.Clear();
                                progress?.Report(("SavingItemsStr", new object[] { }));
                            }
                        }

                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Image download failed for {i.imageUrl}: {ex.Message}");
                    }
                    finally { semaphore.Release(); }
                });

            await Task.WhenAll(downloadTasks);
            //sw.Stop();
            //LoggerService.Log("Perf", $"Parallel image download: {sw.ElapsedMilliseconds}ms | Items: {itemsToProcess.Count(i => i.imageUrl != null)}");


            if (batch.Count > 0)
            {
                try
                {
                    DbService.SaveItemsBatch(batch);
                    progress?.Report(("SavingItemsStr", new object[] { }));
                    batch.Clear();
                }
                catch
                {
                    LoggerService.Log("DbSaveFailed", "Saving items batch failed");
                }
            }
            sw.Stop();
            LoggerService.Log("Api Performance", $"Parallel thread time: {sw.ElapsedMilliseconds}ms | Items processed: {itemsToProcess.Count}");
            progress?.Report(("FinishedItemFetchStr", new object[] { }));
            return batch;
        }

        private static async Task<byte[]> DownloadWithProgressAsync(
            string url,
            Action<long> onBytesRead,
            CancellationToken cancellationToken,
            int speedLimitBytes = 0,
            int maxRetries = 3)
        {
            int attempt = 0;
            while (true)
            {
                try
                {
                    using var response = await httpClient.GetAsync(
                        url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                    if ((int)response.StatusCode == 504 && attempt < maxRetries)
                    {
                        
                        attempt++;
                        await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken);
                        continue;
                    }

                    response.EnsureSuccessStatusCode();

                    using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    using var ms = new MemoryStream();

                    var buffer = new byte[8192];
                    int bytesRead;
                    while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
                    {
                        await ms.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                        onBytesRead(bytesRead);

                        // Apply speed limit if set
                        if (speedLimitBytes > 0)
                        {
                            var delay = (int)(buffer.Length * 1000.0 / speedLimitBytes);
                            if (delay > 0)
                                await Task.Delay(delay, cancellationToken);
                        }
                    }
                    return ms.ToArray();
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception) when (attempt < maxRetries)
                {
                    attempt++;
                    await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken);
                }
            }
        }
    }
}
