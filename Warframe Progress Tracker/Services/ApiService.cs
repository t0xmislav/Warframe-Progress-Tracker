using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Warframe_Progress_Tracker.Services
{
    internal class ApiService
    {
        private static readonly HttpClient httpClient = new HttpClient();

        public static async Task<List<Model.Item>> FetchItemsAsync()
        {
            var response = await httpClient.GetStringAsync("https://api.warframestat.us/items");
            var jsonArray = JArray.Parse(response);

            var items = new List<Model.Item>();
            foreach (var item in jsonArray)
            {
                var masteryReq = item["masteryReq"]?.ToObject<int?>() ?? -1;
                var category = (String)item["category"];
                var excludeFromCodex = item["excludeFromCodex"]?.ToObject<bool>() ?? false;

                if (masteryReq < 0 || category == null || excludeFromCodex) continue;

                items.Add(new Model.Item
                {
                    Name = (String)item["name"],
                    UniqueName = (String)item["uniqueName"],
                    Category = new Model.Category {DisplayName = category},
                    ImageUrl = "https://cdn.warframestat.us/img/" + (String)item["imageName"]
                });
     
            }
            return items;
        }

        public static async Task<JObject> FetchWarframeProfile(string displayName)
        {
            var url = "https://api.warframestat.us/profile/{Uri.EscapeDataString(displayName)}";
            using var client = new HttpClient();
            var response = await client.GetStringAsync(url);
            return JObject.Parse(response);

        }
    }
}
