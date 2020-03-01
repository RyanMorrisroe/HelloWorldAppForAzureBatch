namespace BatchController
{
    public class PoolSettings
    {
        public string PoolId { get; set; }
        public string JobId { get; set; }
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