namespace PocketRSSSync
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using PocketRSSSync.Models;
    using RssFeedParser;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    public class Worker : BackgroundService
    {
        private readonly IConfiguration config;
        private readonly ILogger<Worker> logger;
        private readonly HttpClient client;
        private int count = 0;

        private Auth Auth { get; set; }
        private FeedParser FeedReader { get; set; }


        public Worker(IConfiguration configuration, ILogger<Worker> logger, IHttpClientFactory httpClient)
        {
            this.config = configuration;
            this.logger = logger;
            this.client = httpClient.CreateClient();
            this.Auth = new Auth(config, client, this.logger);
            this.FeedReader = new FeedParser();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var feedReader = new FeedParser();
            
            while (!stoppingToken.IsCancellationRequested)
            {
                var currentItems = await PocketItem.GetPocketItemsAsync(Auth, client);

                var feedUris = config.GetSection("Feeds").Get<List<string>>();

                var taskList = new List<Task>();

                foreach (var feedUri in feedUris)
                {
                    taskList.Add(ProcessFeed(feedUri, currentItems));
                }

                Task.WaitAll(taskList.ToArray());

                logger.LogInformation("{count} total items added to Pocket since program starting. Worker running at: {time}", count, DateTimeOffset.Now);
                await Task.Delay(new TimeSpan(2, 0, 0), stoppingToken);
            }
        }

        private async Task ProcessFeed(string feedUri, List<PocketItem> currentItems)
        {
            try
            {
                var feed = (await FeedReader.ParseFeed(feedUri)).Articles;
                var skipped = feed.Where(a => a.Link.Contains('#')).OrderBy(a => a.Link).ToList();
                var articles = feed.DistinctBy(a => a.Link).Where(a => !a.Link.Contains('#')).ToList();

                foreach (var article in articles)
                {
                    if (currentItems.Select(i => i.Url).Contains(article.Link))
                    {
                        continue;
                    }

                    var pocketItem = new PocketItem()
                    {
                        Title = article.Title,
                        Url = article.Link,
                    };

                    await PocketItem.AddPocketItem(Auth, client, pocketItem);
                    count++;
                }

                foreach (var skip in skipped)
                {
                    logger.LogInformation("Skipped: {}\n{}", skip.Link, skip.Title);
                }
            }

            catch (HttpRequestException ex)
            {
                logger.LogError($"{feedUri} returned {ex.StatusCode}");
            }
        }
    }
}

