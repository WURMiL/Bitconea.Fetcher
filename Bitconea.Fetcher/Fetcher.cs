using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Bitconea.Fetcher
{
    public class Fetcher
    {
        private readonly ILogger<Fetcher> _logger;
        private static readonly ConcurrentDictionary<FetcherJob, object> ActiveJobs = new();
        private static readonly SemaphoreSlim Semaphore;

        //private readonly HttpClient _httpClient;
        //private readonly FetcherJob _fetcherJob;

        static Fetcher()
        {
            Semaphore = new(4);
        }

        public Fetcher(ILogger<Fetcher> logger)
        {
            _logger = logger;
        }

        //public Fetcher(FetcherJob fetcherJob)
        //{
        //    var timeout = _fetcherJob.Timeout != TimeSpan.Zero ? _fetcherJob.Timeout : TimeSpan.FromSeconds(15);

        //    _httpClient = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate }) { Timeout = timeout };
        //    _fetcherJob = fetcherJob;
        //}

        //public Fetcher(HttpClient httpClient, FetcherJob fetcherJob)
        //{
        //    _httpClient = httpClient;
        //    _fetcherJob = fetcherJob;
        //}

        public async Task Fetch(FetcherJob fetcherJob, HttpClient httpClient = null)
        {
            if (httpClient == null)
            {
                var timeout = fetcherJob.Timeout != TimeSpan.Zero ? fetcherJob.Timeout : TimeSpan.FromSeconds(15);
                httpClient = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate }) { Timeout = timeout };
            }

            await Semaphore.WaitAsync();

            if (fetcherJob.WaitForFinishPendingRequestFromSameHost)
            {
                while (ActiveJobs.Keys.Any(x => x.Uri.Host == fetcherJob.Uri.Host))
                {
                    await Task.Delay(100);
                }
                ActiveJobs.TryAdd(fetcherJob, null);
            }

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            //Console.WriteLine($"{DateTime.Now.ToLongTimeString()}, {DateTime.Now.Millisecond}: Start fetching from {_fetcherJob.Uri.OriginalString}");
            _logger.LogInformation("Start fetching from {Url}", fetcherJob.Uri.OriginalString);
            var jobResponse = new FetcherJobResponse();

            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Authorization = fetcherJob.AuthenticationHeaderValue;

            try
            {
                HttpResponseMessage message;

                foreach (var customHeader in fetcherJob.CustomHeaders)
                {
                    httpClient.DefaultRequestHeaders.Add(customHeader.Key, customHeader.Value);
                }

                if (fetcherJob.HttpMethod == HttpMethod.Get)
                {
                    message = await httpClient.GetAsync(fetcherJob.Uri);
                }
                else if (fetcherJob.HttpMethod == HttpMethod.Put)
                {
                    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    message = await httpClient.PutAsync(fetcherJob.Uri, fetcherJob.HttpContent);
                }
                else if (fetcherJob.HttpMethod == HttpMethod.Post)
                {
                    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    message = await httpClient.PostAsync(fetcherJob.Uri, fetcherJob.HttpContent);
                }
                else
                {
                    throw new Exception($"Unsupported HTTP method '{fetcherJob.HttpMethod}'");
                }

                jobResponse.HttpResponseMessage = message;
                jobResponse.ReasonPhrase = message.ReasonPhrase;
                jobResponse.ResponseStatusCode = message.StatusCode;
                jobResponse.RawResponse = await message.Content.ReadAsStringAsync();

                jobResponse.IsSuccessStatusCode = message.IsSuccessStatusCode;

                if (message.IsSuccessStatusCode)
                {
                    if (string.IsNullOrEmpty(jobResponse.RawResponse))
                    {
                        jobResponse.Exception = new Exception("Empty response.");
                        jobResponse.IsSuccessfull = false;
                    }
                    else if (fetcherJob.ExpectJson && jobResponse.JToken == null)
                    {
                        jobResponse.Exception = new Exception("no JSON response");
                        jobResponse.IsSuccessfull = false;
                    }
                    else
                    {
                        //jobResponse.IsJson = message.Content.Headers.ContentType?.MediaType == "application/json" && jobResponse.JToken != null;
                        //Console.WriteLine($"{DateTime.Now.ToLongTimeString()}, {DateTime.Now.Millisecond}: SUCCESS response from from {_fetcherJob.Uri.AbsoluteUri}");
                        _logger.LogInformation("Success response from {Url} with {Duration}ms", fetcherJob.Uri.OriginalString, stopwatch.ElapsedMilliseconds);
                        jobResponse.IsSuccessfull = true;
                    }
                }
                else
                {
                    jobResponse.IsSuccessfull = false;
                }

            }
            catch (Exception ex)
            {
                jobResponse.Exception = ex.InnerException?.InnerException ?? ex.InnerException ?? ex;
                jobResponse.IsSuccessfull = false;
                //Console.WriteLine($"{DateTime.Now.ToLongTimeString()}, {DateTime.Now.Millisecond}: [{_fetcherJob.Uri.AbsoluteUri}] {(ex.InnerException?.InnerException ?? ex.InnerException ?? ex).Message}");
                _logger.LogError(jobResponse.Exception, "Fetching data from {Url} failed", fetcherJob.Uri.OriginalString);
            }
            finally
            {
                ActiveJobs.TryRemove(fetcherJob, out _);
                Semaphore.Release();
            }

            fetcherJob.IsProcessed = true;
            stopwatch.Stop();
            jobResponse.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
            fetcherJob.Response = jobResponse;
        }
    }
}
