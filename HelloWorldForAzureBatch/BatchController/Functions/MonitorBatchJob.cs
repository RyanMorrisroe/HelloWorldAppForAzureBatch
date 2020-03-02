using System;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace BatchController.Functions
{
    public static class MonitorBatchJob
    {
        [FunctionName("MonitorBatchJob")]
        public static async Task Run([OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            Contract.Requires(context != null);
            string jobId = context.GetInput<string>();
            bool jobComplete = await BatchMonitor.IsJobComplete(jobId).ConfigureAwait(true);
            if(jobComplete)
            {
                //Do some stuff, maybe call a pipeline or something
            }
            else
            {
                DateTime nextRun = context.CurrentUtcDateTime.AddMinutes(int.Parse(Environment.GetEnvironmentVariable("BATCH_MONITOR_POLLING_TIME_IN_MINUTES")));
                await context.CreateTimer(nextRun, CancellationToken.None).ConfigureAwait(true);
                context.ContinueAsNew(jobId);
            }
        }
    }
}