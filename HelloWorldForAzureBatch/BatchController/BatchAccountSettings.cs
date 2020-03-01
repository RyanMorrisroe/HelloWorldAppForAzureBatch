using System;
using System.Collections.Generic;

namespace BatchController
{
    public class BatchAccountSettings
    {
        public string AccountName { get; set; }
        public string AccountKey { get; set; }
        public string AccountUrl { get; set; }
        public List<BatchApplicationSettings> Applications { get; } = new List<BatchApplicationSettings>();
    }
}