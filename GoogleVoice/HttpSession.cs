using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Diagnostics;

namespace GoogleVoice
{
    internal class HttpSession
    {
        public enum HttpUserAgent
        {
            Desktop, Mobile, Undefined
        }
        // Chrome 4.0 on Windows 7
        const string DesktopUserAgent = "Mozilla/5.0 (Windows; U; Windows NT 6.1; en-US) AppleWebKit/532.8 (KHTML, like Gecko) Chrome/4.0.302.2 Safari/532.8";
        // Safari on iPhone iOS4
        const string MobileUserAgent = "Mozilla/5.0 (iPhone; U; CPU iPhone OS 4_0 like Mac OS X; en-us) AppleWebKit/532.9 (KHTML, like Gecko) Version/4.0.5 Mobile/8A293 Safari/6531.22.7";

        private CookieContainer Jar = new CookieContainer();

        public void Initialize(Cookie SMSV)
        {
            // 2-step verification cookie.
            Jar.Add(SMSV);
        }
    
        public HttpResult Get(string url, HttpUserAgent UserAgent = HttpUserAgent.Undefined)
        {
            Trace.WriteLine("GET: " + url);
            HttpWebRequest request = HttpWebRequest.Create(url) as HttpWebRequest;
            request.CookieContainer = Jar;
            request.UserAgent = _MakeUserAgent(UserAgent);

            using (HttpWebResponse response = request.GetResponse() as HttpWebResponse)
            {
                return _MakeResponse(response);
            }
        }

        public HttpResult Post(string url, string postData, bool logging = true, HttpUserAgent UserAgent = HttpUserAgent.Undefined)
        {
            if (logging) Trace.WriteLine("POST: " + url);
            byte[] data = Encoding.UTF8.GetBytes(postData);

            HttpWebRequest request = HttpWebRequest.Create(url) as HttpWebRequest;
            request.CookieContainer = Jar;
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.UserAgent = _MakeUserAgent(UserAgent);
            request.AllowAutoRedirect = true;
          
            Stream newStream = request.GetRequestStream();
            newStream.Write(data, 0, data.Length);
            newStream.Flush();
            newStream.Close();

            using (HttpWebResponse response = request.GetResponse() as HttpWebResponse)
            {
                return _MakeResponse(response, logging);
            }
        }

        public HttpResult Post(string url, Dictionary<string, string> postValues, bool logging = true, HttpUserAgent UserAgent = HttpUserAgent.Undefined)
        {
            string postData = "";
            foreach (var kv in postValues)
            {
                postData += string.Format("&{0}={1}", kv.Key, WebUtil.UrlEncode(kv.Value));
            }
            return Post(url, postData, logging, UserAgent);
        }

        public byte[] GetFile(string url, HttpUserAgent UserAgent = HttpUserAgent.Undefined)
        {
            Trace.WriteLine("GET: " + url);
            HttpWebRequest request = HttpWebRequest.Create(url) as HttpWebRequest;
            request.CookieContainer = Jar;
            request.UserAgent = _MakeUserAgent(UserAgent);

            using (HttpWebResponse response = request.GetResponse() as HttpWebResponse)
            {
                using (MemoryStream memoryStream = new MemoryStream(0x10000))
                {
                    using (Stream responseStream = response.GetResponseStream())
                    {
                        byte[] buffer = new byte[0x1000];
                        int bytes;
                        while ((bytes = responseStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            memoryStream.Write(buffer, 0, bytes);
                        }
                        return memoryStream.ToArray();
                    }
                }
            }
        }

        public Cookie GetCookie(string key, string url)
        {
            var KEY = key.ToUpper();
            CookieCollection cc = Jar.GetCookies(new Uri(url));
            foreach (Cookie c in cc)
            {
                
                if (KEY == c.Name.ToUpper()) return c;
            }
            return null;
        }

        public string GetCookieString(string key, string url)
        {
            var cookie = GetCookie(key, url);
            return cookie != null ? cookie.Value : "";
        }

        private HttpResult _MakeResponse(HttpWebResponse response, bool logging = true)
        {
            using (StreamReader reader = new StreamReader(response.GetResponseStream()))
            {
                if (logging) Trace.WriteLine("-> " + response.ResponseUri.ToString());
                return new HttpResult
                {
                    Page = reader.ReadToEnd(),
                    Uri = response.ResponseUri,
                };
            }
        }

        private string _MakeUserAgent(HttpUserAgent ua)
        {
            switch (ua)
            {
                case HttpUserAgent.Mobile:
                    return MobileUserAgent;
                case HttpUserAgent.Desktop:
                    return DesktopUserAgent;
                default:
                    // we'll default to mobile
                    return MobileUserAgent;
            }
        }
    }

    public class HttpResult
    {
        public string Page { get; set; }
        public Uri Uri { get; set; }
    }
}
