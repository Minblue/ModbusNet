using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Xml;
//using TempModbusNet.Services;
using Newtonsoft.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TempModbusNet.Service
{
    public class LikeCountCrawler : IHostedService
    {
        public LikeCountCrawler(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<LikeCountCrawler> logger) =>
        (_httpClientFactory, _configuration, _logger) =
            (httpClientFactory, configuration, logger);

        private readonly System.Timers.Timer _timer = new();
        //private readonly Timer _timer = timer;
        private readonly IEnumerable<string> _links = new string[]
        {
            "https://developer.huawei.com/consumer/cn/forum/topicview?tid=0201308791792470245&fid=23",
            "https://developer.huawei.com/consumer/cn/forum/topicview?tid=0201303654965850166&fid=18",
            "https://developer.huawei.com/consumer/cn/forum/topicview?tid=0201294272503450453&fid=24",
            "https://developer.huawei.com/consumer/cn/forum/topicview?tid=0201294189025490019&fid=17"
        };
        private readonly string _baseUrl = "https://developer.huawei.com/consumer/cn/forum/mid/partnerforumservice/v1/open/getTopicDetail";
        private readonly IHttpClientFactory _httpClientFactory;// = HttpClientFactoryExtensions.;
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;

        /// <summary>
        /// 爬虫
        /// </summary>
        /// <param name="link"></param>
        /// <returns></returns>
        public async Task<int> Crawl(string link)
        {
            using (var httpClient = HttpClientFactoryExtensions.CreateClient(_httpClientFactory))
            {
                var uri = new Uri(link);
                string query = uri.Query;
                //int length = query.IndexOf("&")- query.IndexOf("tid=") +1;
                string topicId = query.Substring(query.IndexOf("tid=")+4, 19);
                //uri.Query//
                //uri.TryReadQueryAsJson(out var queryParams);
                //var topicId = queryParams["tid"].ToString();
                //var topicId = string.Empty;
                int likeCount = -1;
                if (!string.IsNullOrEmpty(topicId))
                {
                    var body = JsonConvert.SerializeObject(
                                new { topicId });
                    uri = new Uri(_baseUrl);
                    var jsonContentType = "application/json";

                    var requestMessage = new HttpRequestMessage
                    {
                        RequestUri = uri,
                        Headers =
                {
                    { "Host", uri.Host }
                },
                        Method = HttpMethod.Post,
                        Content = new StringContent(body)
                    };
                    requestMessage.Content.Headers.ContentType = new MediaTypeWithQualityHeaderValue(jsonContentType);
                    requestMessage.Content.Headers.ContentLength = body.Length;
                    var response = await httpClient.SendAsync(requestMessage);
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        string data = await response.Content.ReadAsStringAsync();
                        //dynamic data = await response.Content.ReadAsAsync<dynamic>();
                        //Console.WriteLine("response: "+data);
                        _logger.LogInformation(message: data);
                        likeCount = 1;
                    }
                }

                return likeCount;
            }
        }

        public async Task Crawl(IEnumerable<string> links)
        {
            await Task.Run(() =>
            {
                Parallel.ForEach(links, async link =>
                {
                    Console.WriteLine($"Crawling link:{link}, ThreadId:{Thread.CurrentThread.ManagedThreadId}");
                    var likeCount = await Crawl(link);
                    Console.WriteLine($"Succeed crawling likecount - {likeCount}, ThreadId:{Thread.CurrentThread.ManagedThreadId}");
                });
            });
        }

        private void OnTimer(object sender, ElapsedEventArgs e)
        {
            _ = Crawl(_links);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _timer.Interval = 5 * 60 * 1000;
            _timer.Elapsed += OnTimer;
            _timer.AutoReset = true;
            _timer.Enabled = true;
            _timer.Start();
            OnTimer(null, null);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            //throw new NotImplementedException();
            _timer.Enabled = false;
            _timer.Stop();
            return Task.CompletedTask;
        }
    }
}
