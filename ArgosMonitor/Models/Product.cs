using System;
using System.Collections.Generic;
using System.Text;

namespace ArgosMonitor
{
    internal class Product
    {
        public string productSku { get; set; }
        public string itemName { get; set; }
        public Dictionary<string, bool> availability { get; set; }

        public Product(string productSku, string itemName)
        {
            this.productSku = productSku;
            this.itemName = itemName;
            availability = new Dictionary<string, bool>();
        }
    }
}
