using System;

namespace BatchController
{
    public class PoolSettings
    {
        public string PoolId { get; set; }
        private string _jobId;
        public string JobId
        {
            get
            {
                return _jobId;
            }
            set
            {
                _jobId = $"{value}_{DateTime.UtcNow.Ticks.ToString()}";
            }
        }
        public string PoolOSFamily { get; set; }
        public string PoolVMSize { get; set; }
        public bool UseAutoscale { get; set; }
        public int TargetVMCount { get; set; }
        public int StartingVMCount { get; set; }
        public int MinVMCount { get; set; }
        public int MaxVMCount { get; set; }
        public bool ShouldDeleteJob { get; set; }
    }
}