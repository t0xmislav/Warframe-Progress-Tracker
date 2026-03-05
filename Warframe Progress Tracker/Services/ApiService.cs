using Newtonsoft.Json.Linq;
using SQLitePCL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
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

        public static async Task<List<Model.Item>> FetchItemsAsync(IProgress<(string key, object[] args)>? progress, 
            CancellationToken cancellationToken = default, HashSet<string>? existingUniqueNames = null)
        {
            progress?.Report(("LoadingFetchingItemsStr", new object[] { }));

            var response = await httpClient.GetStringAsync("https://api.warframestat.us/items");
            var jsonArray = JArray.Parse(response);
            var items = new List<Model.Item>();
            int count = 0;

            foreach (var item in jsonArray)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var uniqueName = (string)item["uniqueName"];
                if (existingUniqueNames.Contains(uniqueName)) 
                {
                    System.Diagnostics.Debug.WriteLine($"Item Exists {uniqueName}");
                    continue;
                }
                System.Diagnostics.Debug.WriteLine("populating...");
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
                        System.Diagnostics.Debug.WriteLine($"Downloading image failed for {imageUrl} with error {ex.Message}");
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
                count++;
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

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    System.Diagnostics.Debug.WriteLine($"Profile not found at {wfStatApi}");
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
                System.Diagnostics.Debug.WriteLine($"Reading Speed limit settings failed {ex.Message}");
            }
            IProgress<double>? imgProgress = new Progress<double>(p =>
            {
                progress.Report(("DownloadingImageProgressStr", new object[] { name, (int)(p * 100) }));
            });

            try
            {
                return await HttpDownloaderUtil.DownloadDataAsync
                    (url,
                    imgProgress,
                    speedLimitKB, cancellationToken);
            }
            catch
            {
                using var client = new HttpClient();
                return await client.GetByteArrayAsync(url);
            }
        }
    }
}
