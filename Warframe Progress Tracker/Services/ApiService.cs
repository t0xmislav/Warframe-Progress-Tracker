using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Warframe.Tracker.Filters;

namespace Warframe_Progress_Tracker.Services
{
    internal class ApiService
    {
        private static readonly HttpClient httpClient = new HttpClient();

        public static async Task<List<Model.Item>> FetchItemsAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        {
            progress?.Report("Fetching item list...");

            var response = await httpClient.GetStringAsync("https://api.warframestat.us/items");
            var jsonArray = JArray.Parse(response);
            var items = new List<Model.Item>();
            int count = 0;
            System.Diagnostics.Debug.WriteLine("After Fetch.");
            foreach (var item in jsonArray)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var uniqueName = (string)item["uniqueName"];
                if (DbService.ItemExists(uniqueName)) 
                {
                    System.Diagnostics.Debug.WriteLine($"Item Exists {uniqueName}");
                    continue;
                }
                System.Diagnostics.Debug.WriteLine("populating...");
                var masteryReq = item["masteryReq"]?.ToObject<int?>() ?? -1;
                var category = (string)item["category"];
                var excludeFromCodex = item["excludeFromCodex"]?.ToObject<bool>() ?? false;
                

                //Skip all items not granting mastery, including mission nodes
                if (!ItemFilter.GrantsMastery(uniqueName, category, masteryReq, excludeFromCodex)) continue;
                count++;
                if(count % 10 == 0)
                {
                    progress?.Report($"Processed {count} items...");
                }
                var masteryPoints = MasteryCalculator.GetMasteryPoints(category, uniqueName);
                var imageName = (string)item["imageName"];
                byte[] imageBytes = null;
                //TODO Multithread image saving
                if (!string.IsNullOrEmpty(imageName))
                {
                    var imageUrl = "https://cdn.warframestat.us/img/" + imageName;
                    try
                    {
                        imageBytes = await DownloadImageAsync(imageUrl);

                    }
                    catch(Exception ex)
                    { 
                        System.Diagnostics.Debug.WriteLine($"Image download failed for {imageUrl}: {ex.Message}");
                    }
                }
               
                items.Add(new Model.Item
                {
                    Name = (String)item["name"],
                    UniqueName = (String)item["uniqueName"],
                    Category = new Model.Category {DisplayName = category},
                    MasteryPoints = masteryPoints,
                    Image = imageBytes
                });
     
            }
            progress?.Report("Finished fetching items.");
            return items;
        }

        public static async Task<JObject> FetchWarframeProfile(string displayName)
        {
            var url = $"https://api.warframestat.us/profile/{Uri.EscapeDataString(displayName)}";
            using var client = new HttpClient();
            var response = await client.GetStringAsync(url);
            return JObject.Parse(response);

        }
        public static async Task<byte[]> DownloadImageAsync(string url)
        {
            using var client = new HttpClient();
            return await client.GetByteArrayAsync(url);
        }
    }
}
