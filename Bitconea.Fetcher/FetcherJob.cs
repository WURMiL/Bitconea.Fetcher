using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;

namespace Bitconea.Fetcher
{
    public class FetcherJob
    {
        public AuthenticationHeaderValue AuthenticationHeaderValue { get; }

        public FetcherJob(string url, bool expectJson = true)
        {
            Url = url;
            ExpectJson = expectJson;
            Created = DateTime.Now;
            CustomHeaders = new Dictionary<string, string>();
        }

        public FetcherJob(string url, TimeSpan timeout) : this(url)
        {
            Timeout = timeout;
        }

        public FetcherJob(string url, AuthenticationHeaderValue authenticationHeaderValue) : this(url)
        {
            AuthenticationHeaderValue = authenticationHeaderValue;
        }

        public FetcherJob(string url, AuthenticationHeaderValue authenticationHeaderValue, TimeSpan timeout) : this(url, authenticationHeaderValue)
        {
            Timeout = timeout;
        }

        public DateTime Created { get; }
        public string Url { get; }
        public bool ExpectJson { get; }

        public Uri Uri => new Uri(Url);
        public bool IsProcessed { get; set; }
        public TimeSpan Timeout { get; }

        public HttpMethod HttpMethod { get; set; } = HttpMethod.Get;
        public HttpContent HttpContent { get; set; }

        public Dictionary<string, string> CustomHeaders { get; }
        public FetcherJobResponse Response { get; set; }
        public bool WaitForFinishPendingRequestFromSameHost { get; set; }

        public void ClearResponse()
        {
            IsProcessed = false;
            Response = null;
        }
    }

}
