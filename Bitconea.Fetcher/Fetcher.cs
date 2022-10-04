using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Bitconea.Fetcher
{
    public class Fetcher
    {
        private static readonly ConcurrentDictionary<FetcherJob, object> ActiveJobs = new();
        private static readonly SemaphoreSlim Semaphore;

        private readonly HttpClient _httpClient;
        private readonly FetcherJob _fetcherJob;

        static Fetcher()
        {
            Semaphore = new(4);
        }

        public Fetcher(FetcherJob fetcherJob)
        {
            var timeout = _fetcherJob.Timeout != TimeSpan.Zero ? _fetcherJob.Timeout : TimeSpan.FromSeconds(15);

            _httpClient = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate }) { Timeout = timeout };
            _fetcherJob = fetcherJob;
        }

        public Fetcher(HttpClient httpClient, FetcherJob fetcherJob)
        {
            _httpClient = httpClient;
            _fetcherJob = fetcherJob;
        }

        public async Task<FetcherJob> Fetch()
        {
            await Semaphore.WaitAsync();

            if (_fetcherJob.WaitForFinishPendingRequestFromSameHost)
            {
                while (ActiveJobs.Keys.Any(x => x.Uri.Host == _fetcherJob.Uri.Host))
                {
                    await Task.Delay(100);
                }
                ActiveJobs.TryAdd(_fetcherJob, null);
            }

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            //Console.WriteLine($"{DateTime.Now.ToLongTimeString()}, {DateTime.Now.Millisecond}: Start fetching from {_fetcherJob.Uri.OriginalString}");
            var jobResponse = new FetcherJobResponse();

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = _fetcherJob.AuthenticationHeaderValue;

            try
            {
                HttpResponseMessage message;

                foreach (var customHeader in _fetcherJob.CustomHeaders)
                    _httpClient.DefaultRequestHeaders.Add(customHeader.Key, customHeader.Value);

                if (_fetcherJob.HttpMethod == HttpMethod.Get)
                {
                    message = await _httpClient.GetAsync(_fetcherJob.Uri);
                }
                else if (_fetcherJob.HttpMethod == HttpMethod.Put)
                {
                    _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    message = await _httpClient.PutAsync(_fetcherJob.Uri, _fetcherJob.HttpContent);
                }
                else if (_fetcherJob.HttpMethod == HttpMethod.Post)
                {
                    _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    message = await _httpClient.PostAsync(_fetcherJob.Uri, _fetcherJob.HttpContent);
                }
                else
                {
                    throw new Exception($"Unsupported HTTP method '{_fetcherJob.HttpMethod}'");
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
                        jobResponse.Exception = new Exception($"Empty response.");
                        jobResponse.IsSuccessfull = false;
                    }
                    else if (_fetcherJob.ExpectJson && jobResponse.JToken == null)
                    {
                        jobResponse.Exception = new Exception($"no JSON response");
                        jobResponse.IsSuccessfull = false;
                    }
                    else
                    {
                        //jobResponse.IsJson = message.Content.Headers.ContentType?.MediaType == "application/json" && jobResponse.JToken != null;
                        //Console.WriteLine($"{DateTime.Now.ToLongTimeString()}, {DateTime.Now.Millisecond}: SUCCESS response from from {_fetcherJob.Uri.AbsoluteUri}");
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
            }
            finally
            {
                ActiveJobs.TryRemove(_fetcherJob, out _);
                Semaphore.Release();
            }

            _fetcherJob.IsProcessed = true;
            stopwatch.Stop();
            jobResponse.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
            _fetcherJob.Response = jobResponse;

            return _fetcherJob;
        }
    }
}
