using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ArgosMonitor.Models
{
    class MonitorTask
    {
        public int taskNumber { get; set; }
        public bool isRunning { get; set; }
        public bool useProxy { get; set; }
        public WebProxy proxy { get; set; }
        public CookieContainer cookies { get; set; }
        public Product product { get; set; }
        public string postcode { get; set; }
        public int interval { get; set; }
        public List<string> webhooks { get; set; }
    }
}
