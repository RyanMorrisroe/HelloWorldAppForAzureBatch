using System;
using System.Collections.Generic;

namespace BatchController
{
    public class BatchAccountSettings
    {
        public string AccountName { get; set; }
        public string AccountKey { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:Uri properties should not be strings", Justification = "Uris screw with the batch api")]
        public string AccountUrl { get; set; }
        public List<BatchApplicationSettings> Applications { get; } = new List<BatchApplicationSettings>();
    }
}