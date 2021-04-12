using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Discord.Webhook;
using Discord;
using System.Text;
using ArgosMonitor.Functions;
using ArgosMonitor.Models;
using Microsoft.VisualBasic;

namespace ArgosMonitor
{
    class Program
    {
        private Dictionary<int, MonitorTask> _monitoredItems = new Dictionary<int, MonitorTask>();
        private readonly Dictionary<WebProxy, int> _proxies = new Dictionary<WebProxy, int>();

        private int requestNum = 0;
        private int discordPings = 0;
        private int errors = 0;

        private bool _showAllLogs = false;
        private int _taskNumber = 1;

        public static Dictionary<string, DateTime> LastNotified;

        private static void Main()
        {
            try
            {
                OutputToFile.WriteLine(@"
  ______   ____  ____  __  __  __             _ _                 
 |  _ \ \ / /\ \/ /\ \/ / |  \/  | ___  _ __ (_) |_ ___  _ __ ___ 
 | |_) \ V /  \  /  \  /  | |\/| |/ _ \| '_ \| | __/ _ \| '__/ __|
 |  _ < | |   /  \  /  \  | |  | | (_) | | | | | || (_) | |  \__ \
 |_| \_\|_|  /_/\_\/_/\_\ |_|  |_|\___/|_| |_|_|\__\___/|_|  |___/
");

                var app = new Program();

                LastNotified = new Dictionary<string, DateTime>();

                app.StartAllTasksAsync();

                Console.ReadLine();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            Console.ReadLine();
        }
        
        public void StartNewTask(MonitorTask task)
        {
            OutputToFile.WriteLine("Grabbing sessionId cookie...");
            if (task.useProxy)
            {
                OutputToFile.WriteLine("[USING PROXY]");
                task.proxy = GetNewProxy();
            }
            var cookies = Argos.GetSessionCookie(task.proxy, task.useProxy); // returns null if proxy is invalid

            while (cookies is null && task.useProxy)
            {
                OutputToFile.WriteLine("Proxy invalid or banned! Retrying with a new proxy...");
                task.proxy = GetNewProxy();
                cookies = Argos.GetSessionCookie(task.proxy, task.useProxy);
            }

            task.cookies = cookies;
            task.taskNumber = _taskNumber;
            task.isRunning = true;
            Task.Run(() => GetAvailability(task));
            _monitoredItems.Add(_taskNumber, task);
        }

        public async void StartAllTasksAsync()
        {
            var tasksToBeStarted = await LoadTasksFromFile();

            foreach (var task in tasksToBeStarted)
            {

                task.taskNumber = _taskNumber;

                StartNewTask(task);

                OutputToFile.WriteLine($"Staggering monitor start up... Waiting 5s");
                Thread.Sleep(5000);
                _taskNumber++;
            }
        }

        public async Task<List<MonitorTask>> LoadTasksFromFile()
        {
            var taskToBeStarted = new List<MonitorTask>();
            dynamic responseObject = JObject.Parse(FormatJson(await File.ReadAllTextAsync(Directory.GetCurrentDirectory() + @"\items.json")));

            for (var i = 0; i < responseObject.items.Count; i++)
            {
                string productSku = responseObject.items[i].productSku;
                int interval = responseObject.items[i].interval * 1000;
                bool useProxy = responseObject.items[i].useProxy;

                var locations = new List<string>();

                for (var l = 0; l < responseObject.items[i].locations.Count; l++)
                {
                    locations.Add(responseObject.items[i].locations[l].postcode.ToString());
                }

                var webhooks = new List<string>();

                for (var w = 0; w < responseObject.items[i].webhooks.Count; w++)
                {
                    webhooks.Add(responseObject.items[i].webhooks[w].url.ToString());
                }

                var productName = await Argos.GetProductName(productSku);

                if (productName == "PRODUCTNOTFOUND")
                {
                    OutputToFile.WriteLine("Failed finding item! Aborting adding of item " + productSku);
                }
                else
                {
                    OutputToFile.WriteLine($"Found item {productSku}! Building monitor task for {productName}");

                    taskToBeStarted.AddRange(locations.Select(postcode => new MonitorTask
                    {
                        isRunning = false,
                        useProxy = useProxy,
                        interval = interval,
                        product = new Product(productSku, productName),
                        webhooks = webhooks,
                        postcode = postcode
                    }));
                }
            }

            return taskToBeStarted;
        }

        private static string FormatJson(string json)
        {
            json = json.Trim().Replace("\r", string.Empty);
            json = json.Trim().Replace("\n", string.Empty);
            json = json.Replace(Environment.NewLine, string.Empty);

            return json;
        }

        public async void GetAvailability(MonitorTask task)
        {
            task.product.availability = new Dictionary<string, bool> {{$"Delivery to {task.postcode}", false}};
            var stopWatch = new Stopwatch();

            OutputToFile.WriteLine($"Product {task.product.productSku} monitor request on thread {task.taskNumber} restarted for postcode {task.postcode}");
            try
            {
                while (_monitoredItems[task.taskNumber].isRunning)
                {
                    stopWatch.Start();

                    var deliveryResponse = await Functions.Monitor.GetResponseFromArgosAsync(task, true);
                    var collectionResponse = await Functions.Monitor.GetResponseFromArgosAsync(task, false);

                    var availability = GetCollectionAvailability(collectionResponse);
                    availability.Add($"Delivery to {task.postcode}", IsItemAvailableForDelivery(deliveryResponse));

                    var newStockLocations = GetNewStockLocations(task.product.availability, availability);

                    stopWatch.Stop();

                    if (newStockLocations.Any())
                    {
                        OutputToFile.WriteLine($"[{task.postcode}] ({DateAndTime.Now}) - New stock locations found for {task.product.itemName}! Notifying discord!");
                        Functions.Discord.NotifyDiscordAsync(task, newStockLocations);
                        discordPings++;
                    }
                    else
                    {
                        OutputToFile.WriteLine($"[{task.postcode}] ({DateAndTime.Now}) - No new stock locations for {task.product.itemName}.");
                    }

                    if (_showAllLogs) OutputToFile.Write(" - request took: " + stopWatch.ElapsedMilliseconds + "ms");
                    requestNum++;
                    task.product.availability = availability;
                    UpdateTitle();
                    stopWatch.Reset();
                    Thread.Sleep(task.interval);
                }
            }
            catch (Exception e)
            {
                OutputToFile.WriteLine($"Failed trying to monitor task {task.taskNumber}. Stack trace: {e.StackTrace}");
                OutputToFile.WriteLine($"Restarting task {task.taskNumber}");

                _monitoredItems[task.taskNumber].isRunning = false;

                errors++;
                UpdateTitle();

                Thread.Sleep(1800000); // wait 30 mins
                Task.Run(() => StartNewTask(task)); 
            }
        }

        private static List<string> GetNewStockLocations(Dictionary<string, bool> oldAvailability, Dictionary<string, bool> newAvailability)
        {
            var newStockLocations = new List<string>();

            foreach (var (key, value) in newAvailability)
            {
                if (!oldAvailability.ContainsKey(key))
                {
                    if (value) newStockLocations.Add(key);
                }
                else if (!oldAvailability[key] && value)
                {
                    newStockLocations.Add(key);
                }
            }

            return newStockLocations;
        }


        public bool IsItemAvailableForDelivery(string input)
        {
            dynamic responseObject = JObject.Parse(input);

            if (responseObject.delivery[0].availability[0].quantityAvailable > 0)
            {
                return !(responseObject.delivery[0].messages.delivery_summary.text).ToString().Contains("not available");
            }

            return false;
        }

        public Dictionary<string, bool> GetCollectionAvailability(string input)
        {
            dynamic responseObject = JObject.Parse(input);

            var storeLocation = new Dictionary<string, bool>();

            for (var i = 0; i < responseObject.stores.Count; i++)
            {
                var available = responseObject.stores[i].availability[0].quantityAvailable > 0;
                var storeName = responseObject.stores[i].storeinfo.legacy_name.ToString();
                storeLocation.Add(storeName, available);
            }

            return storeLocation;
        }

        public WebProxy GetNewProxy()
        {
            if (_proxies.Count is 0)
            {
                var lines = File.ReadAllLines($"{Directory.GetCurrentDirectory()}/proxies.txt");

                foreach (var proxy in lines)
                {
                    var split = proxy.Split(':');

                    try
                    {
                        _proxies?.Add(Parse(split[0], split[1], split[2], split[3]), 0);
                    }
                    catch
                    {
                        try
                        {
                            _proxies?.Add(Parse(split[0], split[1], null, null), 0);
                        }
                        catch
                        {
                            // do nothing
                        }
                    }
                }

                if (!_proxies.Any()) throw new Exception("No proxies in proxy file!");
            }
            
            var random = new Random();
            var randomIndex = random.Next(_proxies.Count);

            KeyValuePair<WebProxy, int> randomProxy;

            do
            {
                randomProxy = _proxies.ElementAt(randomIndex); // could get infinite loop here
            } while (randomProxy.Value > 4);

            OutputToFile.WriteLine($"Trying following proxy: {randomIndex}");

            _proxies[randomProxy.Key]++;

            return randomProxy.Key;
        }


        public static WebProxy Parse(string hostname, string port, string username, string password)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

            var parsedProxy = new WebProxy();

            var ip = Dns.GetHostAddresses(hostname); // will throw exception if it cant resolve IP

            parsedProxy.Address = new Uri("http://" + ip[0] + ":" + port);

            if (username != null)
            {
                parsedProxy.Credentials = new NetworkCredential(username, password);
            }

            return parsedProxy;
        }

        public bool StockChanged(Dictionary<string, bool> prevAvailability, Dictionary<string, bool> newAvailability)
        {
            var changedStock = prevAvailability.Keys.Where(newAvailability.ContainsKey)
                .Where(key => newAvailability[key] && !prevAvailability[key]);

            return changedStock.Any();
        }

        public void UpdateTitle()
        {
            Console.Title = $"Requests Made: {requestNum} | Discord Pings: {discordPings} | Errors: {errors}";
        }
    }
}
