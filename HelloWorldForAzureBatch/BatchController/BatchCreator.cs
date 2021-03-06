﻿using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage;
using Microsoft.Azure.Batch.Auth;
using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Common;

namespace BatchController
{
    public static class BatchCreator
    {
        public static async Task<BatchCreationResponse> CreateBatchJob(ILogger log)
        {
            Contract.Requires(log != null);

            PoolSettings poolSettings = new PoolSettings()
            {
                PoolId = Environment.GetEnvironmentVariable("POOL_ID"),
                JobId = Environment.GetEnvironmentVariable("POOL_JOB_ID"),
                PoolOSFamily = Environment.GetEnvironmentVariable("POOL_OS_FAMILY"),
                PoolVMSize = Environment.GetEnvironmentVariable("POOL_VM_SIZE"),
                UseAutoscale = bool.Parse(Environment.GetEnvironmentVariable("POOL_USE_AUTOSCALE")),
                ShouldDeleteJob = bool.Parse(Environment.GetEnvironmentVariable("POOL_SHOULD_DELETE_JOB"))
            };

            if (poolSettings.UseAutoscale)
            {
                poolSettings.StartingVMCount = int.Parse(Environment.GetEnvironmentVariable("POOL_STARTING_VM_COUNT"));
                poolSettings.MinVMCount = int.Parse(Environment.GetEnvironmentVariable("POOL_MIN_VM_COUNT"));
                poolSettings.MaxVMCount = int.Parse(Environment.GetEnvironmentVariable("POOL_MAX_VM_COUNT"));
            }
            else
            {
                poolSettings.TargetVMCount = int.Parse(Environment.GetEnvironmentVariable("POOL_TARGET_VM_COUNT"));
            }

            StorageAccountSettings storageSettings = new StorageAccountSettings()
            {
                AccountName = Environment.GetEnvironmentVariable("BATCH_STORAGE_ACCOUNT_NAME"),
                AccountKey = Environment.GetEnvironmentVariable("BATCH_STORAGE_ACCOUNT_KEY"),
                InputContainerName = Environment.GetEnvironmentVariable("BATCH_STORAGE_INPUT_CONTAINER_NAME"),
                OutputContainerName = Environment.GetEnvironmentVariable("BATCH_STORAGE_OUTPUT_CONTAINER_NAME")
            };

            BatchAccountSettings batchSettings = new BatchAccountSettings()
            {
                AccountName = Environment.GetEnvironmentVariable("BATCH_ACCOUNT_NAME"),
                AccountKey = Environment.GetEnvironmentVariable("BATCH_ACCOUNT_KEY"),
                AccountUrl = Environment.GetEnvironmentVariable("BATCH_ACCOUNT_URL")
            };
            batchSettings.Applications.Add(
                new BatchApplicationSettings()
                {
                    Id = Environment.GetEnvironmentVariable("BATCH_APPLICATION_ID"),
                    Version = Environment.GetEnvironmentVariable("BATCH_APPLICATION_VERSION"),
                    Executable = Environment.GetEnvironmentVariable("BATCH_APPLICATION_EXECUTABLE")
                }
            );

            CloudBlobClient blobClient = CreateBlobClient(storageSettings.AccountName, storageSettings.AccountKey);
            CloudBlobContainer inputContainer = blobClient.GetContainerReference(storageSettings.InputContainerName);
            if (!(await inputContainer.ExistsAsync().ConfigureAwait(true)))
            {
                log.LogError($"Blob storage input container, {storageSettings.InputContainerName}, does not exist");
                throw new Exception("Blob storage input container does not exist");
            }
            CloudBlobContainer outputContainer = blobClient.GetContainerReference(storageSettings.OutputContainerName);
            if (!(await outputContainer.ExistsAsync().ConfigureAwait(true)))
            {
                log.LogInformation("Creating blob storage output container");
                await outputContainer.CreateIfNotExistsAsync().ConfigureAwait(true);
            }

            BatchSharedKeyCredentials batchCredentials = new BatchSharedKeyCredentials(batchSettings.AccountUrl, batchSettings.AccountName, batchSettings.AccountKey);
            using (BatchClient batchClient = BatchClient.Open(batchCredentials))
            {
                ImageReference imageReference = CreateImageReference();
                VirtualMachineConfiguration vmConfiguration = CreateVirtualMachineReference(imageReference);

                log.LogInformation($"Creating pool {poolSettings.PoolId}...");
                await CreateBatchPool(batchClient, batchSettings.Applications, vmConfiguration, poolSettings, log).ConfigureAwait(true);

                log.LogInformation($"Creating job {poolSettings.JobId}...");
                await CreateBatchJob(batchClient, poolSettings, log).ConfigureAwait(true);

                log.LogInformation("Creating tasks...");
                List<CloudTask> tasks = new List<CloudTask>();
                int counter = 1;
                await foreach (ResourceFile resourceFile in GetFilesFromContainer(inputContainer))
                {
                    foreach (BatchApplicationSettings application in batchSettings.Applications)
                    {
                        string taskID = string.Format("Task_{0}_{1}_{2}", application.Id, counter, DateTime.UtcNow.Ticks);
                        string inputFileName = resourceFile.FilePath;
                        string inputFilePath = "%AZ_BATCH_TASK_WORKING_DIR%\\" + inputFileName;
                        string outputDirectory = "%AZ_BATCH_TASK_WORKING_DIR%\\output\\";
                        string executablePath = "%AZ_BATCH_APP_PACKAGE_" + application.Id + "#" + application.Version + "%\\" + application.Executable;
                        string taskCommandLine = string.Format(@"cmd /c {0} {1} {2}", executablePath, inputFilePath, outputDirectory);

                        CloudTask task = new CloudTask(taskID, taskCommandLine)
                        {
                            ResourceFiles = new List<ResourceFile>() { resourceFile },
                            OutputFiles = new List<OutputFile>()
                            {
                                GetTaskOutputFile(outputDirectory, inputFileName, outputContainer),
                                GetStandardOutFile(poolSettings.JobId, taskID, outputContainer)
                            }
                        };
                        tasks.Add(task);
                        counter++;
                    }
                }
                await batchClient.JobOperations.AddTaskAsync(poolSettings.JobId, tasks).ConfigureAwait(true);
                BatchCreationResponse response = new BatchCreationResponse()
                {
                    PoolId = poolSettings.PoolId,
                    JobId = poolSettings.JobId
                };
                tasks.ForEach(x => response.TaskIds.Add(x.Id));
                return response;
            }
        }

        public static CloudBlobClient CreateBlobClient(string storageAccountName, string storageAccountKey)
        {
            string connectionString = $"DefaultEndpointsProtocol=https;AccountName={storageAccountName};AccountKey={storageAccountKey};";
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);
            return storageAccount.CreateCloudBlobClient();
        }

        public static ImageReference CreateImageReference()
        {
            return new ImageReference(
                publisher: "MicrosoftWindowsServer",
                offer: "WindowsServer",
                sku: "2019-datacenter-smalldisk",
                version: "latest");
        }

        public static VirtualMachineConfiguration CreateVirtualMachineReference(ImageReference imageReference)
        {
            Contract.Requires(imageReference != null);

            return new VirtualMachineConfiguration(
                imageReference: imageReference,
                nodeAgentSkuId: "batch.node.windows amd64");
        }

        public static async Task CreateBatchPool(BatchClient batchClient, List<BatchApplicationSettings> applications, VirtualMachineConfiguration vmConfiguration, PoolSettings poolSettings, ILogger log)
        {
            Contract.Requires(batchClient != null);
            Contract.Requires(applications != null);
            Contract.Requires(vmConfiguration != null);
            Contract.Requires(poolSettings != null);
            Contract.Requires(log != null);

            try
            {
                CloudPool pool = batchClient.PoolOperations.CreatePool(
                        poolId: poolSettings.PoolId,
                        virtualMachineSize: poolSettings.PoolVMSize,
                        virtualMachineConfiguration: vmConfiguration
                    );
                if (poolSettings.UseAutoscale)
                {
                    pool.AutoScaleEnabled = true;
                    pool.AutoScaleEvaluationInterval = TimeSpan.FromMinutes(5);
                    pool.AutoScaleFormula = @$"
                    startingNumberOfVMs = {poolSettings.StartingVMCount};
                    minNumberOfVMs = {poolSettings.MinVMCount};
                    maxNumberOfVMs = {poolSettings.MaxVMCount};
                    pendingTaskSamplePercent = $PendingTasks.GetSamplePercent(180 * TimeInterval_Second);
                    pendingTaskSamples = pendingTaskSamplePercent < 70 ? startingNumberOfVMs : avg($PendingTasks.GetSample(180 * TimeInterval_Second));
                    $TargetDedicatedNodes = max(minNumberOfVMs, min(maxNumberOfVMs, pendingTaskSamples));
                    $NodeDeallocationOption = taskcompletion;
                    ";
                }
                else
                {
                    pool.TargetDedicatedComputeNodes = poolSettings.TargetVMCount;
                }
                List<ApplicationPackageReference> applicationReferences = new List<ApplicationPackageReference>();
                foreach (BatchApplicationSettings application in applications)
                {
                    applicationReferences.Add(
                        new ApplicationPackageReference()
                        {
                            ApplicationId = application.Id,
                            Version = application.Version
                        }
                    );
                };
                pool.ApplicationPackageReferences = applicationReferences;
                await pool.CommitAsync().ConfigureAwait(true);
            }
            catch (BatchException exception)
            {
                if (exception.RequestInformation?.BatchError?.Code == BatchErrorCodeStrings.PoolExists)
                {
                    log.LogInformation($"Pool {poolSettings.PoolId} already exists");
                }
                else
                {
                    throw;
                }
            }
        }

        public static async Task CreateBatchJob(BatchClient batchClient, PoolSettings poolSettings, ILogger log)
        {
            Contract.Requires(batchClient != null);
            Contract.Requires(poolSettings != null);
            Contract.Requires(log != null);

            try
            {
                CloudJob job = batchClient.JobOperations.CreateJob();
                job.Id = poolSettings.JobId;
                job.PoolInformation = new PoolInformation() { PoolId = poolSettings.PoolId };
                if (poolSettings.ShouldDeleteJob)
                {
                    job.OnAllTasksComplete = OnAllTasksComplete.TerminateJob;
                }
                await job.CommitAsync().ConfigureAwait(true);
            }
            catch (BatchException exception)
            {
                if (exception.RequestInformation?.BatchError?.Code == BatchErrorCodeStrings.JobExists)
                {
                    log.LogInformation($"Job {poolSettings.JobId} already exists");
                }
                else
                {
                    throw;
                }
            }
        }

        public static async IAsyncEnumerable<ResourceFile> GetFilesFromContainer(CloudBlobContainer container)
        {
            Contract.Requires(container != null);

            int? maxResultsPerRequest = 500;
            BlobContinuationToken continuationToken = null;

            do
            {
                BlobResultSegment response = await container.ListBlobsSegmentedAsync(string.Empty, true, BlobListingDetails.None, maxResultsPerRequest, continuationToken, null, null).ConfigureAwait(true);
                continuationToken = response.ContinuationToken;

                foreach (CloudBlob blob in response.Results.OfType<CloudBlob>())
                {
                    SharedAccessBlobPolicy sasPolicy = new SharedAccessBlobPolicy()
                    {
                        SharedAccessExpiryTime = DateTime.UtcNow.AddHours(1),
                        Permissions = SharedAccessBlobPermissions.Read | SharedAccessBlobPermissions.List
                    };
                    string sasBlobToken = blob.GetSharedAccessSignature(sasPolicy);
                    string blobsasUri = string.Format("{0}{1}", blob.Uri, sasBlobToken);
                    yield return ResourceFile.FromUrl(blobsasUri, blob.Name);
                }
            }
            while (continuationToken != null);
        }

        public static OutputFile GetTaskOutputFile(string outputDirectory, string fileName, CloudBlobContainer outputContainer)
        {
            Contract.Requires(outputDirectory != null);
            Contract.Requires(fileName != null);
            Contract.Requires(outputContainer != null);

            SharedAccessBlobPolicy sasPolicy = new SharedAccessBlobPolicy()
            {
                SharedAccessExpiryTime = DateTime.UtcNow.AddHours(1),
                Permissions = SharedAccessBlobPermissions.Read | SharedAccessBlobPermissions.List | SharedAccessBlobPermissions.Write
            };
            string sasContainerToken = outputContainer.GetSharedAccessSignature(sasPolicy);
            string containerSasUri = string.Format("{0}{1}", outputContainer.Uri, sasContainerToken);
            return new OutputFile(
                filePattern: $"{outputDirectory}{fileName}",
                destination: new OutputFileDestination(new OutputFileBlobContainerDestination(containerSasUri)),
                uploadOptions: new OutputFileUploadOptions(OutputFileUploadCondition.TaskSuccess));
        }

        public static OutputFile GetStandardOutFile(string jobID, string taskID, CloudBlobContainer outputContainer, string filePattern = @"..\std*.txt")
        {
            Contract.Requires(jobID != null);
            Contract.Requires(taskID != null);
            Contract.Requires(outputContainer != null);
            Contract.Requires(filePattern != null);

            SharedAccessBlobPolicy sasPolicy = new SharedAccessBlobPolicy()
            {
                SharedAccessExpiryTime = DateTime.UtcNow.AddHours(1),
                Permissions = SharedAccessBlobPermissions.Read | SharedAccessBlobPermissions.List | SharedAccessBlobPermissions.Write
            };
            string sasContainerToken = outputContainer.GetSharedAccessSignature(sasPolicy);
            string containerSasUri = string.Format("{0}{1}", outputContainer.Uri, sasContainerToken);
            string outputContainerPath = $"errorlogs\\{jobID.ToLowerInvariant()}\\{taskID.ToLowerInvariant()}";
            return new OutputFile(
                filePattern: filePattern,
                destination: new OutputFileDestination(new OutputFileBlobContainerDestination(containerSasUri, outputContainerPath)),
                uploadOptions: new OutputFileUploadOptions(OutputFileUploadCondition.TaskFailure)); //Switch to completion if you always want logs saved
        }
    }
}