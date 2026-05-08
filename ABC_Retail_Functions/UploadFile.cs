using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Storage.Files.Shares;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ABC_Retail_Functions
{
    public class UploadFile
    {
        private readonly ILogger _logger;

        public UploadFile(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<UploadFile>();
        }

        [Function("UploadFile")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request to upload a file to Azure Files (ASP.NET Core model).");

            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                JObject? data = JsonConvert.DeserializeObject<JObject>(requestBody);

                if (data == null || data["ShareName"] == null || data["DirectoryName"] == null || data["FileName"] == null || data["FileBase64"] == null)
                {
                    return new BadRequestObjectResult("Please pass ShareName, DirectoryName, FileName, and FileBase64 in the request body.");
                }

                string shareName = data["ShareName"]?.ToString() ?? string.Empty;
                string directoryName = data["DirectoryName"]?.ToString() ?? string.Empty;
                string fileName = data["FileName"]?.ToString() ?? string.Empty;
                string fileBase64 = data["FileBase64"]?.ToString() ?? string.Empty;

                if (string.IsNullOrEmpty(shareName) || string.IsNullOrEmpty(directoryName) || string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(fileBase64))
                {
                    return new BadRequestObjectResult("Invalid input data.");
                }

                byte[] fileBytes = Convert.FromBase64String(fileBase64);

                var connectionString = Environment.GetEnvironmentVariable("AzureStorage");
                if (string.IsNullOrEmpty(connectionString))
                {
                    return new StatusCodeResult(StatusCodes.Status500InternalServerError);
                }

                var shareClient = new ShareClient(connectionString, shareName);
                await shareClient.CreateIfNotExistsAsync();
                var directoryClient = shareClient.GetDirectoryClient(directoryName);
                await directoryClient.CreateIfNotExistsAsync();
                var fileClient = directoryClient.GetFileClient(fileName);

                using (var ms = new MemoryStream(fileBytes))
                {
                    await fileClient.CreateAsync(ms.Length);
                    await fileClient.UploadRangeAsync(new Azure.HttpRange(0, ms.Length), ms);
                }

                return new OkObjectResult("Success");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file.");
                return new ObjectResult($"Error: {ex.Message}") { StatusCode = 500 };
            }
        }
    }
}
