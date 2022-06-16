using System;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json.Linq;

namespace Bitconea.Fetcher
{
    public class FetcherJobResponse
    {
        //private static readonly ILog Log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private JToken _jToken;
        public string RawResponse { get; set; }
        public HttpStatusCode? ResponseStatusCode { get; set; }
        public bool IsSuccessStatusCode { get; set; }
        public bool IsSuccessfull { get; set; }
        public bool IsJson { get; set; }
        public Exception Exception { get; set; }
        public long ElapsedMilliseconds { get; set; }
        public string ReasonPhrase { get; set; }
        public HttpResponseMessage HttpResponseMessage { get; set; }
        public JToken JToken
        {
            get
            {
                if (_jToken != null)
                    return _jToken;

                if (IsSuccessStatusCode)
                {
                    try
                    {
                        return _jToken = JToken.Parse(RawResponse);
                    }
                    catch (Exception e)
                    {
                        if (!string.IsNullOrEmpty(RawResponse))
                        {
                            //Log.Error($"Error parsing raw data to json. RawResponse.Length={RawResponse.Length}, truncated RawResponse(1000)='{new string(RawResponse.Take(1000).ToArray())}'", e);
                        }

                        return _jToken = null;
                    }
                }
                return _jToken = null;

            }
        }

        public string ErrorMessage
        {
            get
            {
                if (Exception != null)
                    return Exception.Message;

                if (!IsSuccessfull)
                    return ResponseStatusCode + "(" + (int?)ResponseStatusCode + ")";

                return "";
            }
        }

        public void ThrowExceptionIfExist()
        {
            if (!IsSuccessfull && Exception != null)
                throw Exception;
        }
    }
}
