using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;
using Azure.Storage.Queues;
using Azure.Storage.Files.Shares;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using ABC_Retail.Models;
using Newtonsoft.Json;
using System.Net.Http;
using System.Text;

namespace ABC_Retail.Services
{
    public class AzureStorageService
    {
        private readonly string _connectionString;
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly string _functionsUrl;

        public AzureStorageService(IConfiguration configuration)
        {
            _connectionString = configuration.GetSection("AzureStorage")["ConnectionString"] ?? "";
            _functionsUrl = configuration.GetSection("AzureStorage")["FunctionsUrl"] ?? "";
        }

        // --- BLOB STORAGE ---
        public async Task UploadBlobAsync(string containerName, string blobName, System.IO.Stream content)
        {
            using (var ms = new System.IO.MemoryStream())
            {
                await content.CopyToAsync(ms);
                var fileBase64 = Convert.ToBase64String(ms.ToArray());

                var payload = new
                {
                    ContainerName = containerName,
                    BlobName = blobName,
                    FileBase64 = fileBase64
                };

                var json = JsonConvert.SerializeObject(payload);
                var contentStr = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_functionsUrl}UploadBlob", contentStr);
                response.EnsureSuccessStatusCode();
            }
        }

        public async Task DeleteBlobAsync(string containerName, string blobName)
        {
            var serviceClient = new BlobServiceClient(_connectionString);
            var containerClient = serviceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobName);
            await blobClient.DeleteIfExistsAsync();
        }

        public async Task<List<string>> ListBlobsAsync(string containerName)
        {
            var blobs = new List<string>();
            if (string.IsNullOrEmpty(_connectionString)) return blobs;

            var serviceClient = new BlobServiceClient(_connectionString);
            var containerClient = serviceClient.GetBlobContainerClient(containerName);
            
            if (await containerClient.ExistsAsync())
            {
                await foreach (var blobItem in containerClient.GetBlobsAsync())
                {
                    var blobClient = containerClient.GetBlobClient(blobItem.Name);
                    
                    // Generate a SAS token for private access (expires in 1 hour)
                    if (blobClient.CanGenerateSasUri)
                    {
                        var sasBuilder = new BlobSasBuilder()
                        {
                            BlobContainerName = containerName,
                            BlobName = blobItem.Name,
                            Resource = "b",
                            ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
                        };
                        sasBuilder.SetPermissions(BlobSasPermissions.Read);
                        Uri sasUri = blobClient.GenerateSasUri(sasBuilder);
                        blobs.Add(sasUri.ToString());
                    }
                    else
                    {
                        // Fallback to direct URI if SAS can't be generated
                        blobs.Add(blobClient.Uri.ToString());
                    }
                }
            }
            return blobs;
        }

        // --- TABLE STORAGE ---
        public async Task AddTableEntityAsync(string tableName, CustomerProfile customer)
        {
            var payload = new
            {
                TableName = tableName,
                PartitionKey = customer.PartitionKey,
                RowKey = customer.RowKey,
                FirstName = customer.FirstName,
                LastName = customer.LastName,
                Email = customer.Email,
                PhoneNumber = customer.PhoneNumber
            };

            var json = JsonConvert.SerializeObject(payload);
            var contentStr = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_functionsUrl}StoreTableInfo", contentStr);
            response.EnsureSuccessStatusCode();
        }

        // --- TABLE STORAGE: ORDERS ---
        public async Task AddOrderAsync(OrderViewModel order)
        {
            var payload = new
            {
                TableName = "Orders",
                PartitionKey = "RetailOrders",
                RowKey = order.OrderId,
                OrderDate = order.OrderDate,
                CustomerName = order.CustomerName,
                Amount = (double)order.Amount,
                Status = order.Status
            };

            var json = JsonConvert.SerializeObject(payload);
            var contentStr = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_functionsUrl}StoreTableInfo", contentStr);
            response.EnsureSuccessStatusCode();
        }

        public async Task<List<OrderViewModel>> GetOrdersAsync()
        {
            var orders = new List<OrderViewModel>();
            try
            {
                var serviceClient = new TableServiceClient(_connectionString);
                var tableClient = serviceClient.GetTableClient("Orders");
                await tableClient.CreateIfNotExistsAsync();

                await foreach (var entity in tableClient.QueryAsync<TableEntity>())
                {
                    orders.Add(new OrderViewModel
                    {
                        OrderId = entity.RowKey,
                        OrderDate = entity.GetDateTimeOffset("OrderDate")?.DateTime ?? DateTime.UtcNow,
                        CustomerName = entity.GetString("CustomerName"),
                        Amount = (decimal)(entity.GetDouble("Amount") ?? 0.0),
                        Status = entity.GetString("Status")
                    });
                }
            }
            catch { }
            return orders;
        }

        public async Task<OrderViewModel?> GetOrderAsync(string orderId)
        {
            try
            {
                var serviceClient = new TableServiceClient(_connectionString);
                var tableClient = serviceClient.GetTableClient("Orders");
                var response = await tableClient.GetEntityAsync<TableEntity>("RetailOrders", orderId);
                var entity = response.Value;

                return new OrderViewModel
                {
                    OrderId = entity.RowKey,
                    OrderDate = entity.GetDateTimeOffset("OrderDate")?.DateTime ?? DateTime.UtcNow,
                    CustomerName = entity.GetString("CustomerName"),
                    Amount = (decimal)(entity.GetDouble("Amount") ?? 0.0),
                    Status = entity.GetString("Status")
                };
            }
            catch { return null; }
        }

        public async Task<List<CustomerProfile>> GetTableEntitiesAsync(string tableName)
        {
            var customers = new List<CustomerProfile>();
            var serviceClient = new TableServiceClient(_connectionString);
            var tableClient = serviceClient.GetTableClient(tableName);
            await tableClient.CreateIfNotExistsAsync();

            await foreach (var entity in tableClient.QueryAsync<TableEntity>())
            {
                customers.Add(new CustomerProfile
                {
                    PartitionKey = entity.PartitionKey,
                    RowKey = entity.RowKey,
                    FirstName = entity.GetString("FirstName"),
                    LastName = entity.GetString("LastName"),
                    Email = entity.GetString("Email"),
                    PhoneNumber = entity.GetString("PhoneNumber")
                });
            }
            return customers;
        }

        // --- TABLE STORAGE: ALERTS ---
        public async Task<List<AlertViewModel>> GetAlertsAsync()
        {
            var alerts = new List<AlertViewModel>();
            var serviceClient = new TableServiceClient(_connectionString);
            var tableClient = serviceClient.GetTableClient("SystemAlerts");
            await tableClient.CreateIfNotExistsAsync();

            await foreach (var entity in tableClient.QueryAsync<TableEntity>())
            {
                alerts.Add(new AlertViewModel
                {
                    Id = int.Parse(entity.RowKey),
                    Severity = entity.GetString("Severity"),
                    Message = entity.GetString("Message"),
                    Timestamp = entity.GetDateTimeOffset("Timestamp")?.DateTime ?? DateTime.UtcNow,
                    IsResolved = entity.GetBoolean("IsResolved") ?? false
                });
            }

            // If empty, add some initial real data to Azure
            if (alerts.Count == 0)
            {
                string[] severities = { "Critical", "Warning", "Info" };
                for (int i = 1; i <= 5; i++)
                {
                    var entity = new TableEntity("Alerts", i.ToString())
                    {
                        { "Severity", severities[i % 3] },
                        { "Message", $"System Health Check #{i}" },
                        { "Timestamp", DateTime.UtcNow.AddMinutes(-i * 30) },
                        { "IsResolved", false }
                    };
                    await tableClient.AddEntityAsync(entity);
                    alerts.Add(new AlertViewModel { Id = i, Severity = (string)entity["Severity"], Message = (string)entity["Message"], Timestamp = (DateTime)entity["Timestamp"], IsResolved = false });
                }
            }
            return alerts;
        }

        public async Task UpdateAlertStatusAsync(int id, bool isResolved)
        {
            var serviceClient = new TableServiceClient(_connectionString);
            var tableClient = serviceClient.GetTableClient("SystemAlerts");
            var entity = await tableClient.GetEntityAsync<TableEntity>("Alerts", id.ToString());
            entity.Value["IsResolved"] = isResolved;
            await tableClient.UpdateEntityAsync(entity.Value, entity.Value.ETag);
        }

        // --- TABLE STORAGE: AUTH ---
        public async Task<AdminUser?> GetAdminUserAsync(string username)
        {
            try
            {
                var serviceClient = new TableServiceClient(_connectionString);
                var tableClient = serviceClient.GetTableClient("AdminUsers");
                await tableClient.CreateIfNotExistsAsync();

                var response = await tableClient.GetEntityAsync<TableEntity>("Staff", username);
                var entity = response.Value;

                return new AdminUser
                {
                    RowKey = entity.RowKey,
                    PasswordHash = entity.GetString("PasswordHash"),
                    FullName = entity.GetString("FullName")
                };
            }
            catch { return null; }
        }

        public async Task AddAdminUserAsync(AdminUser user)
        {
            var payload = new
            {
                TableName = "AdminUsers",
                PartitionKey = user.PartitionKey,
                RowKey = user.RowKey,
                PasswordHash = user.PasswordHash,
                FullName = user.FullName
            };

            var json = JsonConvert.SerializeObject(payload);
            var contentStr = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_functionsUrl}StoreTableInfo", contentStr);
            response.EnsureSuccessStatusCode();
        }

        // --- QUEUE STORAGE ---
        public async Task SendQueueMessageAsync(string queueName, string message)
        {
            var payload = new
            {
                QueueName = queueName,
                Message = message
            };

            var json = JsonConvert.SerializeObject(payload);
            var contentStr = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_functionsUrl}SendMessageToQueue", contentStr);
            response.EnsureSuccessStatusCode();
        }

        public async Task<int> GetQueueDepthAsync(string queueName)
        {
            var queueClient = new QueueClient(_connectionString, queueName);
            if (await queueClient.ExistsAsync())
            {
                var properties = await queueClient.GetPropertiesAsync();
                return properties.Value.ApproximateMessagesCount;
            }
            return 0;
        }

        public async Task<List<QueueMessageViewModel>> ReceiveMessagesAsync(string queueName)
        {
            var messages = new List<QueueMessageViewModel>();
            var queueClient = new QueueClient(_connectionString, queueName);

            if (await queueClient.ExistsAsync())
            {
                var receivedMessages = await queueClient.ReceiveMessagesAsync(maxMessages: 10, visibilityTimeout: TimeSpan.FromSeconds(30));

                foreach (var msg in receivedMessages.Value)
                {
                    messages.Add(new QueueMessageViewModel
                    {
                        MessageId = msg.MessageId,
                        PopReceipt = msg.PopReceipt,
                        Payload = msg.MessageText,
                        EnqueuedTime = msg.InsertedOn?.DateTime ?? DateTime.UtcNow,
                        RetryCount = (int)msg.DequeueCount,
                        QueueName = queueName
                    });
                }
            }
            return messages;
        }

        public async Task DeleteQueueMessageAsync(string queueName, string messageId, string popReceipt)
        {
            var queueClient = new QueueClient(_connectionString, queueName);
            await queueClient.DeleteMessageAsync(messageId, popReceipt);
        }

        // --- FILE STORAGE (SHARES) ---
        public async Task UploadFileAsync(string shareName, string directoryName, string fileName, System.IO.Stream content)
        {
            using (var ms = new System.IO.MemoryStream())
            {
                await content.CopyToAsync(ms);
                var fileBase64 = Convert.ToBase64String(ms.ToArray());

                var payload = new
                {
                    ShareName = shareName,
                    DirectoryName = directoryName,
                    FileName = fileName,
                    FileBase64 = fileBase64
                };

                var json = JsonConvert.SerializeObject(payload);
                var contentStr = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_functionsUrl}UploadFile", contentStr);
                response.EnsureSuccessStatusCode();
            }
        }
    }
}
