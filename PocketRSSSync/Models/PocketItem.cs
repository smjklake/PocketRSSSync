namespace PocketRSSSync.Models
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using System.Collections.Generic;
    using System.Dynamic;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Net.Http.Json;
    using System.Threading.Tasks;

    internal class PocketItem
    {
        public string Url { get; set; }
        public string Title { get; set; }

        public static async Task<List<PocketItem>> GetPocketItemsAsync(Auth auth, HttpClient client)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "https://getpocket.com/v3/get");

            request.Headers.Add("X-Accept", "application/json");
            request.Content = JsonContent.Create(
                new
                {
                    consumer_key = auth.ConsumerKey,
                    access_token = auth.AccessToken,
                    detailType = "complete",
                    state = "all"
                });
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var jsonSettings = new JsonSerializerSettings();
            jsonSettings.Converters.Add(new ExpandoObjectConverter());
            jsonSettings.Converters.Add(new StringEnumConverter());

            dynamic dynConfig = JsonConvert.DeserializeObject<ExpandoObject>(await response.Content.ReadAsStringAsync(), jsonSettings);

            var items = new List<PocketItem>();

            foreach (var listItem in dynConfig.list)
            {
                items.Add(new PocketItem()
                {
                    Title = listItem.Value.given_title,
                    Url = listItem.Value.given_url
                });
            }

            return items;
        }

        public static async Task AddPocketItem(Auth auth, HttpClient client, PocketItem item)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "https://getpocket.com/v3/add");

            request.Headers.Add("X-Accept", "application/json");
            request.Content = JsonContent.Create(
                new
                {
                    consumer_key = auth.ConsumerKey,
                    access_token = auth.AccessToken,
                    url = item.Url,
                    title = item.Title
                });
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }
    }
}
