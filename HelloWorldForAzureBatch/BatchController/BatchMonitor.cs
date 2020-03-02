using System;
using Microsoft.Azure.Batch.Auth;
using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Common;

namespace BatchController
{
    public static class BatchMonitor
    {
        //This cannot be async because if it is the durable function will crash out
        public static bool IsJobComplete(string jobId)
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
                CloudJob jobInfo = batchClient.JobOperations.GetJob(jobId);
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