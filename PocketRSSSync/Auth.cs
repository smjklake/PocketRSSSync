namespace PocketRSSSync
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using System.Dynamic;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Net.Http.Json;
    using System.Threading.Tasks;
    public class Auth
    {
        private readonly IConfiguration config;
        private readonly ILogger logger;
        private readonly HttpClient client;

        public string AuthenticatedUsername { get; private set; }
        public string AccessToken { get; private set; }
        public string ConsumerKey { get; private set; }

        public Auth(IConfiguration config, HttpClient client, ILogger logger)
        {
            this.config = config;
            this.logger = logger;
            this.client = client;
            GetAuthentication().Wait();
        }

        private async Task GetAuthentication()
        {
            var accessToken = config["AuthToken"];
            var userName = config["AuthUser"];
            var consumerKey = config["ConsumerKey"];

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                // throw if null

                var redirectUri = config["AuthRedirectURI"];
                if (string.IsNullOrWhiteSpace(redirectUri))
                {
                    throw new System.Exception("Didn't parse config");
                }

                var request = new HttpRequestMessage(HttpMethod.Post, "https://getpocket.com/v3/oauth/request");

                request.Headers.Add("X-Accept", "application/json");
                request.Content = JsonContent.Create(new { consumer_key = consumerKey, redirect_uri = redirectUri });
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                var resp = await client.SendAsync(request);
                resp.EnsureSuccessStatusCode();

                var def = new { code = "", state = "" };

                var obj = JsonConvert.DeserializeAnonymousType(await resp.Content.ReadAsStringAsync(), def);

                logger.LogInformation($"https://getpocket.com/auth/authorize?request_token={obj.code}&redirect_uri={redirectUri}");

                using (var listener = new HttpListener())
                {
                    listener.Prefixes.Add($"{redirectUri}/");
                    listener.Start();
                    logger.LogInformation("Listening for Authentication Response...");

                    HttpListenerContext context = listener.GetContext();
                    HttpListenerRequest sentRequest = context.Request;
                    HttpListenerResponse response = context.Response;
                    string responseString = $"Successfully authenticated with Pocket!";
                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                    response.ContentLength64 = buffer.Length;
                    System.IO.Stream output = response.OutputStream;
                    output.Write(buffer, 0, buffer.Length);
                    output.Close();
                    listener.Stop();
                }

                request = new HttpRequestMessage(HttpMethod.Post, "https://getpocket.com/v3/oauth/authorize");

                request.Headers.Add("X-Accept", "application/json");
                request.Content = JsonContent.Create(new { consumer_key = consumerKey, code = obj.code });
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                resp = await client.SendAsync(request);
                resp.EnsureSuccessStatusCode();

                var authDef = new { access_token = "", username = "" };

                var auth = JsonConvert.DeserializeAnonymousType(await resp.Content.ReadAsStringAsync(), authDef);

                logger.LogInformation($"User {auth.username} authenticated with pocket.");

                accessToken = auth.access_token;
                userName = auth.username;

                var appSettingsPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
                var json = File.ReadAllText(appSettingsPath);
                var jsonSettings = new JsonSerializerSettings();
                jsonSettings.Converters.Add(new ExpandoObjectConverter());
                jsonSettings.Converters.Add(new StringEnumConverter());

                dynamic dynConfig = JsonConvert.DeserializeObject<ExpandoObject>(json, jsonSettings);

                dynConfig.AuthToken = accessToken;
                dynConfig.AuthUser = userName;

                var newJson = JsonConvert.SerializeObject(dynConfig, Formatting.Indented, jsonSettings);
                File.WriteAllText(appSettingsPath, newJson);
            }

            ConsumerKey = consumerKey;
            AccessToken = accessToken;
            AuthenticatedUsername = userName;            
        }
    }
}
