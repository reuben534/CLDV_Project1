using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Data.Tables;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ABC_Retail_Functions
{
    public class StoreTableInfo
    {
        private readonly ILogger _logger;

        public StoreTableInfo(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<StoreTableInfo>();
        }

        [Function("StoreTableInfo")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
        {
            _logger.LogInformation("StoreTableInfo function started (ASP.NET Core model).");

            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                _logger.LogInformation($"Body length: {requestBody.Length}");

                JObject? data = JsonConvert.DeserializeObject<JObject>(requestBody);

                if (data == null || data["TableName"] == null || data["PartitionKey"] == null || data["RowKey"] == null)
                {
                    _logger.LogWarning("Missing required fields in request body.");
                    return new BadRequestObjectResult("Please pass TableName, PartitionKey, and RowKey in the request body.");
                }

                string tableName = data["TableName"]?.ToString() ?? string.Empty;
                string partitionKey = data["PartitionKey"]?.ToString() ?? string.Empty;
                string rowKey = data["RowKey"]?.ToString() ?? string.Empty;

                _logger.LogInformation($"Target Table: {tableName}, PK: {partitionKey}, RK: {rowKey}");

                var connectionString = Environment.GetEnvironmentVariable("AzureStorage");
                if (string.IsNullOrEmpty(connectionString))
                {
                    _logger.LogError("AzureStorage connection string is missing.");
                    return new StatusCodeResult(StatusCodes.Status500InternalServerError);
                }

                var serviceClient = new TableServiceClient(connectionString);
                var tableClient = serviceClient.GetTableClient(tableName);

                _logger.LogInformation("Ensuring table exists...");
                await tableClient.CreateIfNotExistsAsync();

                _logger.LogInformation("Preparing entity...");
                var entity = new TableEntity(partitionKey, rowKey);

                foreach (var property in data)
                {
                    if (property.Key != "TableName" && property.Key != "PartitionKey" && property.Key != "RowKey")
                    {
                        var value = property.Value;
                        if (value is JValue jValue)
                        {
                            // Assign the underlying primitive value
                            entity[property.Key] = jValue.Value;
                        }
                        else if (value != null)
                        {
                            // Serialize complex objects as JSON strings to avoid Table Storage errors
                            entity[property.Key] = value.ToString(Formatting.None);
                        }
                    }
                }

                _logger.LogInformation("Upserting entity...");
                await tableClient.UpsertEntityAsync(entity);

                _logger.LogInformation("Success.");
                return new OkObjectResult("Success");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in StoreTableInfo: {ex.Message}");
                return new ObjectResult($"Error: {ex.Message}") { StatusCode = 500 };
            }
        }
    }
}
