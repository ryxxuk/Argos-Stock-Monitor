using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ArgosMonitor.Functions
{
    public class Argos
    {
        public static async Task<string> GetProductName(string sku)
        {
            string title;

            switch (sku)
            {
                case "8349024":
                    return "Sony PlayStation 5 Digital Console";
                case "8349000":
                    return "Sony PlayStation 5 Console";
            }

            try
            {
                var url = $"https://www.argos.co.uk/product/{sku}";

                using var handler = new HttpClientHandler
                {
                    AutomaticDecompression = DecompressionMethods.GZip,
                };
                
                using var client = new HttpClient(handler);

                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri(url)
                };

                request.Headers.Add("Accept", "application/json");
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/87.0.4280.66 Safari/537.36");
                
                var response = await client.SendAsync(request);

                var source = await response.Content.ReadAsStringAsync();

                title = Regex.Match(source, @"\<title\b[^>]*\>\s*(?<Title>[\s\S]*?)\</title\>", RegexOptions.IgnoreCase).Groups["Title"].Value;

                title = Regex.Match(title, @"Buy (.*?) \|", RegexOptions.IgnoreCase).Groups[1].Value;
            }
            catch
            {
                title = "PRODUCTNOTFOUND";
            }

            return title;
        }

        public static CookieContainer GetSessionCookie(WebProxy proxy, bool useProxy)
        {
            var request = (HttpWebRequest)WebRequest.Create("https://www.argos.co.uk/");
            if (useProxy)
            {
                request.Proxy = proxy;
                request.Credentials = CredentialCache.DefaultNetworkCredentials;
            }

            request.CookieContainer = new CookieContainer();
            request.Method = "GET";
            request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/86.0.4240.198 Safari/537.36";

            var resp = new HttpWebResponse();
            CookieContainer initialCookies;

            try
            {
                resp = (HttpWebResponse)request.GetResponse();
                if (resp.StatusCode != HttpStatusCode.OK) throw new Exception($"Status code from proxy: {resp.StatusCode}");
                initialCookies = request.CookieContainer;
            }
            catch
            {
                OutputToFile.WriteLine("IP temp banned from www.argos.co.uk");
                resp.Close();
                resp.Dispose();
                return null;
            }

            request = (HttpWebRequest)WebRequest.Create("https://www.argos.co.uk/cis/refresh");
            request.CookieContainer = initialCookies;
            request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/86.0.4240.198 Safari/537.36";
            request.Method = "POST";

            if (useProxy) request.Proxy = proxy;

            resp = (HttpWebResponse)request.GetResponse();

            OutputToFile.WriteLine("Session id: " + resp.Headers.Get("sessionId"));

            resp.Close();
            resp.Dispose();

            return request.CookieContainer;
        }
    }
}
