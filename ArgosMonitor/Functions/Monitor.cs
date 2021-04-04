using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ArgosMonitor.Models;

namespace ArgosMonitor.Functions
{
    internal class Monitor
    {
        public static async Task<string> GetResponseFromArgosAsync(MonitorTask task, bool delivery)
        {
            var baseUrl = "https://www.argos.co.uk/stores/api/orchestrator/v0/cis-locator/availability?";

            if (delivery)
            {
                baseUrl += "maxDistance=50&maxResults=10&skuQty={0}_1&channel=web_pdp&timestamp={1}&postcode={2}";
            }
            else
            {
                baseUrl += "maxDistance=100&maxResults=50&skuQty={0}_1&save=pdp-ss%3A%2F&ssm=true&timestamp={1}&origin={2}";
            }

            var unixTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
            var url = string.Format(baseUrl, task.product.productSku, unixTimestamp, task.postcode);

            using var handler = new HttpClientHandler
            {
                UseCookies = true,
                CookieContainer = task.cookies,
                AutomaticDecompression = DecompressionMethods.GZip,
            };

            if (task.useProxy)
            {
                handler.Proxy = task.proxy;
                handler.Credentials = CredentialCache.DefaultNetworkCredentials;
            }

            using var client = new HttpClient(handler);

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(url)
            };

            request.Headers.Add("Accept", "application/json");
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/87.0.4280.66 Safari/537.36");
            request.Headers.Add("sessionId", "");

            var response = await client.SendAsync(request);
            var attempts = 0;

            while (!response.IsSuccessStatusCode)
            {
                if (attempts > 3) return null;
                OutputToFile.WriteLine("Failed getting response from Argos! Waiting 1 minute and trying again.");
                Thread.Sleep(60000);
                response = await client.SendAsync(request);
                attempts++;
            }

            return await response.Content.ReadAsStringAsync();
        }
    }
}
