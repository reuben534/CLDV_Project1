using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Storage.Queues;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ABC_Retail_Functions
{
    public class ProcessQueueMessage
    {
        private readonly ILogger _logger;

        public ProcessQueueMessage(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ProcessQueueMessage>();
        }

        [Function("SendMessageToQueue")]
        public async Task<IActionResult> SendMessage([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request to send a message to the queue (ASP.NET Core model).");

            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                JObject? data = JsonConvert.DeserializeObject<JObject>(requestBody);

                if (data == null || data["QueueName"] == null || data["Message"] == null)
                {
                    return new BadRequestObjectResult("Please pass QueueName and Message in the request body.");
                }

                string queueName = data["QueueName"]?.ToString() ?? string.Empty;
                string message = data["Message"]?.ToString() ?? string.Empty;

                if (string.IsNullOrEmpty(queueName) || string.IsNullOrEmpty(message))
                {
                    return new BadRequestObjectResult("Invalid input data.");
                }

                var connectionString = Environment.GetEnvironmentVariable("AzureStorage");
                if (string.IsNullOrEmpty(connectionString))
                {
                    return new StatusCodeResult(StatusCodes.Status500InternalServerError);
                }
                var queueClient = new QueueClient(connectionString, queueName);
                await queueClient.CreateIfNotExistsAsync();
                await queueClient.SendMessageAsync(message);

                return new OkObjectResult("Success");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message to queue.");
                return new ObjectResult($"Error: {ex.Message}") { StatusCode = 500 };
            }
        }

        [Function("ReadQueueMessage")]
        public void ReadMessage([QueueTrigger("order-processing", Connection = "AzureStorage")] string myQueueItem)
        {
            _logger.LogInformation($"C# Queue trigger function processed: {myQueueItem}");
            // Additional processing logic can go here
        }
    }
}
