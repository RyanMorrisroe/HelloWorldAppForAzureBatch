using System.Diagnostics.Contracts;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;


namespace BatchController.Functions
{
    public static class StartMonitorBatchJob
    {
        [FunctionName("StartMonitorBatchJob")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req, [DurableClient] IDurableOrchestrationClient starter, ILogger log)
        {
            Contract.Requires(req != null);
            Contract.Requires(starter != null);

            log.LogInformation("Function started processing the request");
            string requestBody;
            using(StreamReader reader = new StreamReader(req.Body))
            {
                requestBody = await reader.ReadToEndAsync().ConfigureAwait(true);
                req.Body = new MemoryStream(Encoding.ASCII.GetBytes(requestBody)); //You have to do this because reading the body screws up the request and CreateCheckStatusResponse will thrown an exception. This is a .NET Core bug.
            }
            BatchCreationResponse data = JsonConvert.DeserializeObject<BatchCreationResponse>(requestBody);
            MonitorBatchJobInput input = new MonitorBatchJobInput()
            {
                JobId = data.JobId
            };
            string instanceId = await starter.StartNewAsync("MonitorBatchJob", input).ConfigureAwait(true);
            log.LogInformation($"Started monitoring orchestration function with Id = {instanceId}");
            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}