using System;
using System.Threading.Tasks;
using Microsoft.Azure.Batch.Auth;
using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Common;

namespace BatchController
{
    public static class BatchMonitor
    {
        public static async Task<bool> IsJobComplete(string jobId)
        {
            BatchAccountSettings batchSettings = new BatchAccountSettings()
            {
                AccountName = Environment.GetEnvironmentVariable("BATCH_ACCOUNT_NAME"),
                AccountKey = Environment.GetEnvironmentVariable("BATCH_ACCOUNT_KEY"),
                AccountUrl = Environment.GetEnvironmentVariable("BATCH_ACCOUNT_URL")
            };

            BatchSharedKeyCredentials batchCredentials = new BatchSharedKeyCredentials(batchSettings.AccountUrl, batchSettings.AccountName, batchSettings.AccountKey);
            using (BatchClient batchClient = BatchClient.Open(batchCredentials))
            {
                CloudJob jobInfo = await batchClient.JobOperations.GetJobAsync(jobId).ConfigureAwait(true);
                if(jobInfo.State == JobState.Completed)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }
    }
}