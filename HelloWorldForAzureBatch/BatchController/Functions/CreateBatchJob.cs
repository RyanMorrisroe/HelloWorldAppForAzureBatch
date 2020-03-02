using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace BatchController.Functions
{
    public static class CreateBatchJob
    {
        [FunctionName("CreateBatchJob")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequest req, ILogger log)
        {
            log.LogInformation("Function started processing the request");
            try
            {
                BatchCreationResponse response = await BatchCreator.CreateBatchJob(log).ConfigureAwait(true);
                return new OkObjectResult(response);
            }
            #pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
            #pragma warning restore CA1031 // Do not catch general exception types
            {
                log.LogError(ex, ex.Message);
                return new BadRequestResult();
            }
            finally
            {
                log.LogInformation("Function finished processing the request");
            }
        }
    }
}