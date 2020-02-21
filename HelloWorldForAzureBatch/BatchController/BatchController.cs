using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage;
using Microsoft.Azure.Batch.Auth;
using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Common;
using System.Collections.Generic;
using System.Linq;

namespace BatchController
{
    public static class BatchController
    {
        [FunctionName("BatchController")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string name = req.Query["name"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.name;

            return name != null
                ? (ActionResult)new OkObjectResult($"Hello, {name}")
                : new BadRequestObjectResult("Please pass a name on the query string or in the request body");
        }

        private static async Task Main(ILogger log)
        {
            PoolSettings poolSettings = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("PoolSettings.json")
                .Build()
                .Get<PoolSettings>();

            string storageAccountName = Environment.GetEnvironmentVariable("BATCH_STORAGE_ACCOUNT_NAME");
            string storageAccountKey = Environment.GetEnvironmentVariable("BATCH_STORAGE_ACCOUNT_KEY");
            string inputContainerName = Environment.GetEnvironmentVariable("BATCH_STORAGE_INPUT_CONTAINER_NAME");
            string outputContainerName = Environment.GetEnvironmentVariable("BATCH_STORAGE_OUTPUT_CONTAINER_NAME");

            CloudBlobClient blobClient = CreateBlobClient(storageAccountName, storageAccountKey);
            CloudBlobContainer inputContainer = blobClient.GetContainerReference(inputContainerName);
            if(!(await inputContainer.ExistsAsync()))
            {
                log.LogError($"Blob storage input container, {inputContainerName}, does not exist");
                throw new Exception("Blob storage input container does not exist");
            }
            CloudBlobContainer outputContainer = blobClient.GetContainerReference(outputContainerName);
            if(!(await outputContainer.ExistsAsync()))
            {
                log.LogInformation("Creating blob storage output container");
                await outputContainer.CreateIfNotExistsAsync();
            }
            
            string batchAccountName = Environment.GetEnvironmentVariable("BATCH_ACCOUNT_NAME");
            string batchAccountKey = Environment.GetEnvironmentVariable("BATCH_ACCOUNT_KEY");
            string batchAccountUrl = Environment.GetEnvironmentVariable("BATCH_ACCOUNT_URL");

            BatchSharedKeyCredentials batchCredentials = new BatchSharedKeyCredentials(batchAccountUrl, batchAccountName, batchAccountKey);
            using(BatchClient batchClient = BatchClient.Open(batchCredentials))
            {
                ImageReference imageReference = CreateImageReference();
                VirtualMachineConfiguration vmConfiguration = CreateVirtualMachineReference(imageReference);

                log.LogInformation($"Creating pool {poolSettings.PoolId}...");
                await CreateBatchPool(batchClient, vmConfiguration, poolSettings, log);

                log.LogInformation($"Creating job {poolSettings.JobId}...");
                await CreateBatchJob(batchClient, poolSettings, log);

                log.LogInformation("Creating tasks...");
                List<CloudTask> tasks = new List<CloudTask>();
                int counter = 1;
                await foreach(ResourceFile resourceFile in GetFilesFromContainer(inputContainer))
                {
                    string taskID = string.Format("Task{0}", counter);
                    string inputFileName = resourceFile.FilePath;
                    string outputDirectory = "//output//";
                    string taskCommandLine = string.Format("BatchProgram.exe {0} {1}", inputFileName, outputDirectory);

                    CloudTask task = new CloudTask(taskID, taskCommandLine)
                    {
                        ResourceFiles = new List<ResourceFile>() { resourceFile },
                        OutputFiles = new List<OutputFile>() { GetTaskOutputFile(outputDirectory, resourceFile.FilePath.Replace("\\input\\" ,""), outputContainer) }
                    };
                    tasks.Add(task);
                }
                await batchClient.JobOperations.AddTaskAsync(poolSettings.JobId, tasks);

                TimeSpan timeout = TimeSpan.FromMinutes(10);
                IEnumerable<CloudTask> addedTasks = batchClient.JobOperations.ListTasks(poolSettings.JobId);
                batchClient.Utilities.CreateTaskStateMonitor().WaitAll(addedTasks, TaskState.Completed, timeout);
                
                if(poolSettings.ShouldDeleteJob)
                {
                    batchClient.JobOperations.DeleteJob(poolSettings.JobId);
                }
            }
        }

        private static CloudBlobClient CreateBlobClient(string storageAccountName, string storageAccountKey)
        {
            string connectionString = $"DefaultEndpointsProtocol=https;AccountName={storageAccountName};AccountKey={storageAccountKey};";
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);
            return storageAccount.CreateCloudBlobClient();
        }

        private static ImageReference CreateImageReference()
        {
            return new ImageReference(
                publisher: "MicrosoftWindowsServer",
                offer: "WindowsServer",
                sku: "2019-datacenter-smalldisk",
                version: "latest");
        }

        private static VirtualMachineConfiguration CreateVirtualMachineReference(ImageReference imageReference)
        {
            return new VirtualMachineConfiguration(
                imageReference: imageReference,
                nodeAgentSkuId: "batch.node.windows amd64");
        }

        private static async Task CreateBatchPool(BatchClient batchClient, VirtualMachineConfiguration vmConfiguration, PoolSettings poolSettings, ILogger log)
        {
            try
            {
                CloudPool pool = batchClient.PoolOperations.CreatePool(
                        poolId: poolSettings.PoolId,
                        targetDedicatedComputeNodes: poolSettings.PoolNodeCount,
                        virtualMachineSize: poolSettings.PoolVMSize,
                        virtualMachineConfiguration: vmConfiguration
                    );
                pool.ApplicationPackageReferences.Add(new ApplicationPackageReference() { ApplicationId = Environment.GetEnvironmentVariable("BATCH_APPLICATION_ID"), Version = Environment.GetEnvironmentVariable("BATCH_APPLICATION_VERSION") });
                await pool.CommitAsync();
            }
            catch(BatchException exception)
            {
                if(exception.RequestInformation?.BatchError?.Code == BatchErrorCodeStrings.PoolExists)
                {
                    log.LogInformation($"Pool {poolSettings.PoolId} already exists");
                }
                else
                {
                    throw;
                }
            }
        }

        private static async Task CreateBatchJob(BatchClient batchClient, PoolSettings poolSettings, ILogger log)
        {
            try
            {
                CloudJob job = batchClient.JobOperations.CreateJob();
                job.Id = poolSettings.JobId;
                job.PoolInformation = new PoolInformation() { PoolId = poolSettings.PoolId };
                await job.CommitAsync();
            }
            catch(BatchException exception)
            {
                if(exception.RequestInformation?.BatchError?.Code == BatchErrorCodeStrings.JobExists)
                {
                    log.LogInformation($"Job {poolSettings.JobId} already exists");
                }
                else
                {
                    throw;
                }
            }
        }

        private static async IAsyncEnumerable<ResourceFile> GetFilesFromContainer(CloudBlobContainer container)
        {
            int? maxResultsPerRequest = 500;
            BlobContinuationToken continuationToken = null;

            do
            {
                BlobResultSegment response = await container.ListBlobsSegmentedAsync(string.Empty, true, BlobListingDetails.None, maxResultsPerRequest, continuationToken, null, null);
                continuationToken = response.ContinuationToken;

                foreach (CloudBlob blob in response.Results.OfType<CloudBlob>())
                {
                    SharedAccessBlobPolicy sasPolicy = new SharedAccessBlobPolicy()
                    {
                        SharedAccessExpiryTime = DateTime.UtcNow.AddHours(1),
                        Permissions = SharedAccessBlobPermissions.Read
                    };
                    string sasBlobToken = blob.GetSharedAccessSignature(sasPolicy);
                    string blobsasUri = string.Format("{0}{1}", blob.Uri, sasBlobToken);
                    yield return ResourceFile.FromUrl(blobsasUri, $"\\input\\{blob.Name}");
                }
            }
            while (continuationToken != null);
        }

        private static OutputFile GetTaskOutputFile(string outputDirectory, string fileName, CloudBlobContainer outputContainer)
        {
            SharedAccessBlobPolicy sasPolicy = new SharedAccessBlobPolicy()
            {
                SharedAccessExpiryTime = DateTime.UtcNow.AddHours(1),
                Permissions = SharedAccessBlobPermissions.Read | SharedAccessBlobPermissions.Write
            };
            string sasContainerToken = outputContainer.GetSharedAccessSignature(sasPolicy);
            string containersasUri = string.Format("{0}{1}", outputContainer.Uri, sasContainerToken);
            return new OutputFile(
                filePattern: $"{outputDirectory}{fileName}",
                destination: new OutputFileDestination(new OutputFileBlobContainerDestination(containersasUri)),
                uploadOptions: new OutputFileUploadOptions(OutputFileUploadCondition.TaskSuccess));
        }
    }
}
