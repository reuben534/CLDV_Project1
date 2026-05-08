using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ABC_Retail_Functions
{
    public class UploadBlob
    {
        private readonly ILogger _logger;

        public UploadBlob(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<UploadBlob>();
        }

        [Function("UploadBlob")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request to upload a blob (ASP.NET Core model).");

            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                JObject? data = JsonConvert.DeserializeObject<JObject>(requestBody);

                if (data == null || data["ContainerName"] == null || data["BlobName"] == null || data["FileBase64"] == null)
                {
                    return new BadRequestObjectResult("Please pass ContainerName, BlobName, and FileBase64 in the request body.");
                }

                string containerName = data["ContainerName"]?.ToString() ?? string.Empty;
                string blobName = data["BlobName"]?.ToString() ?? string.Empty;
                string fileBase64 = data["FileBase64"]?.ToString() ?? string.Empty;

                if (string.IsNullOrEmpty(containerName) || string.IsNullOrEmpty(blobName) || string.IsNullOrEmpty(fileBase64))
                {
                    return new BadRequestObjectResult("Invalid input data.");
                }

                byte[] fileBytes = Convert.FromBase64String(fileBase64);

                var connectionString = Environment.GetEnvironmentVariable("AzureStorage");
                if (string.IsNullOrEmpty(connectionString))
                {
                    return new StatusCodeResult(StatusCodes.Status500InternalServerError);
                }

                var blobServiceClient = new BlobServiceClient(connectionString);
                var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
                await containerClient.CreateIfNotExistsAsync();
                var blobClient = containerClient.GetBlobClient(blobName);

                using (var ms = new MemoryStream(fileBytes))
                {
                    await blobClient.UploadAsync(ms, overwrite: true);
                }

                return new OkObjectResult("Success");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading blob.");
                return new ObjectResult($"Error: {ex.Message}") { StatusCode = 500 };
            }
        }
    }
}
