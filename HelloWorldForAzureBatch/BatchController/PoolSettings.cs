using System.Text;

namespace BatchController
{
    public class PoolSettings
    {
        public string PoolId { get; set; }
        public string JobId { get; set; }
        public int PoolNodeCount { get; set; }
        public string PoolOSFamily { get; set; }
        public string PoolVMSize { get; set; }
        public bool ShouldDeleteJob { get; set; }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            AddSetting(sb, nameof(PoolId), PoolId);
            AddSetting(sb, nameof(JobId), JobId);
            AddSetting(sb, nameof(PoolNodeCount), PoolNodeCount);
            AddSetting(sb, nameof(PoolOSFamily), PoolOSFamily);
            AddSetting(sb, nameof(PoolVMSize), PoolVMSize);
            AddSetting(sb, nameof(ShouldDeleteJob), ShouldDeleteJob);

            return sb.ToString();
        }

        private static void AddSetting(StringBuilder sb, string settingName, object settingValue)
        {
            sb.AppendFormat("{0} = {1}", settingName, settingValue).AppendLine();
        }
    }
}
