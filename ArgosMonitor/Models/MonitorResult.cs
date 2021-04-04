using System;
using System.Collections.Generic;
using System.Text;

namespace ArgosMonitor.Models
{
    class MonitorResult
    {
        public MonitorResult(int timeTaken, Dictionary<string, bool> availability, bool inStock, Product product)
        {
            this.timeTaken = timeTaken;
            this.availability = availability;
            this.inStock = inStock;
            this.product = product;
        }

        public Product product { get; set; }
        public bool inStock { get; set; }
        public Dictionary<string, bool> availability { get; set; }
        public int timeTaken { get; set; }

    }
}
