using System.Collections.Generic;

namespace BatchController
{
    public class BatchControllerResponse
    {
        public string PoolId {get;set;}
        public string JobId { get; set; }
        public List<string> TaskIds { get; } = new List<string>();
    }
}