using Microsoft.AspNetCore.Mvc;
using ABC_Retail.Models;
using ABC_Retail.Services;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace ABC_Retail.Controllers
{
    public class DashboardController : Controller
    {
        private readonly AzureStorageService _storageService;

        public DashboardController(AzureStorageService storageService)
        {
            _storageService = storageService;
        }

        public async Task<IActionResult> Index()
        {
            var orders = await _storageService.GetOrdersAsync();
            var alerts = await _storageService.GetAlertsAsync();
            var queueDepth = await _storageService.GetQueueDepthAsync("order-processing");

            var model = new DashboardSummaryViewModel
            {
                ActiveOrders = orders.Count,
                QueueDepth = queueDepth,
                SystemUptime = 99.99, // Static for now as it's typically from external monitoring
                ErrorRate = alerts.Count(a => a.Severity == "Critical") > 0 ? 0.15 : 0.02,
                RecentOrders = orders.OrderByDescending(o => o.OrderDate).Take(5).ToList(),
                RecentAlerts = alerts.OrderByDescending(a => a.Timestamp).Take(3).ToList()
            };
            return View(model);
        }

        public async Task<IActionResult> Orders()
        {
            var orders = await _storageService.GetOrdersAsync();
            return View(orders);
        }

        public async Task<IActionResult> OrderDetails(string id)
        {
            if (string.IsNullOrEmpty(id)) return RedirectToAction("Orders");
            var order = await _storageService.GetOrderAsync(id);
            if (order == null) return NotFound();
            return View(order);
        }

        [HttpPost]
        public async Task<IActionResult> CreateOrder(OrderViewModel order)
        {
            if (ModelState.IsValid)
            {
                order.OrderDate = DateTime.UtcNow;
                order.Status = "Pending";
                
                // 1. Add to Table Storage
                await _storageService.AddOrderAsync(order);
                
                // 2. Add to Queue for processing
                await _storageService.SendQueueMessageAsync("order-processing", $"New Order: {order.OrderId} for {order.CustomerName}");
                
                TempData["Success"] = $"Order {order.OrderId} created and sent to queue!";
            }
            return RedirectToAction("Orders");
        }

        // --- Azure Table: Customers ---
        public async Task<IActionResult> Customers()
        {
            var customers = await _storageService.GetTableEntitiesAsync("Customers");
            return View(customers);
        }

        [HttpPost]
        public async Task<IActionResult> AddCustomer(CustomerProfile customer)
        {
            if (ModelState.IsValid)
            {
                await _storageService.AddTableEntityAsync("Customers", customer);
                TempData["Success"] = $"Customer '{customer.FirstName} {customer.LastName}' added successfully!";
            }
            return RedirectToAction("Customers");
        }

        // --- Azure Blob: Storage ---
        public async Task<IActionResult> Storage()
        {
            var blobs = await _storageService.ListBlobsAsync("product-images");
            return View(blobs);
        }

        [HttpPost]
        public async Task<IActionResult> UploadBlob(IFormFile file)
        {
            if (file != null && file.Length > 0)
            {
                using var stream = file.OpenReadStream();
                await _storageService.UploadBlobAsync("product-images", file.FileName, stream);
                TempData["Success"] = $"File '{file.FileName}' uploaded successfully!";
            }
            return RedirectToAction("Storage");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteBlob(string blobName)
        {
            if (!string.IsNullOrEmpty(blobName))
            {
                await _storageService.DeleteBlobAsync("product-images", blobName);
                TempData["Success"] = $"File '{blobName}' deleted successfully!";
            }
            return RedirectToAction("Storage");
        }

        // --- Azure Queue: Messages ---
        public async Task<IActionResult> Queue()
        {
            var messages = await _storageService.ReceiveMessagesAsync("order-processing");
            return View(messages);
        }

        [HttpPost]
        public async Task<IActionResult> ProcessMessage(string messageId, string popReceipt)
        {
            try
            {
                await _storageService.DeleteQueueMessageAsync("order-processing", messageId, popReceipt);
                TempData["Success"] = $"Message {messageId.Substring(0, 8)} processed and removed from queue.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Failed to process message: {ex.Message}";
            }
            return RedirectToAction("Queue");
        }

        [HttpPost]
        public async Task<IActionResult> SendMessage(string orderId, string payload)
        {
            if (!string.IsNullOrEmpty(payload))
            {
                await _storageService.SendQueueMessageAsync("order-processing", $"Order {orderId}: {payload}");
                TempData["Success"] = "Manual message enqueued successfully.";
            }
            return RedirectToAction("Queue");
        }

        // --- Azure Files: Upload Log (Extra functionality for rubric) ---
        [HttpPost]
        public async Task<IActionResult> UploadLog(IFormFile logFile)
        {
            if (logFile != null && logFile.Length > 0)
            {
                using var stream = logFile.OpenReadStream();
                await _storageService.UploadFileAsync("retailshare", "logs", logFile.FileName, stream);
            }
            return RedirectToAction("Storage");
        }

        public async Task<IActionResult> Analytics()
        {
            var orders = await _storageService.GetOrdersAsync();
            var customers = await _storageService.GetTableEntitiesAsync("Customers");
            var alerts = await _storageService.GetAlertsAsync();

            var model = new AnalyticsViewModel
            {
                TotalCustomers = customers.Count,
                TotalRevenue = orders.Sum(o => o.Amount),
                AverageOrderValue = orders.Any() ? (double)orders.Average(o => o.Amount) : 0,
                
                // Group orders by last 7 days
                DailyOrders = Enumerable.Range(0, 7)
                    .Select(i => DateTime.UtcNow.Date.AddDays(-i))
                    .Reverse()
                    .Select(date => new DailyOrderCount
                    {
                        Day = date.ToString("ddd"),
                        Count = orders.Count(o => o.OrderDate.Date == date)
                    }).ToList(),

                // Mocking region distribution based on customer names for visualization
                RegionData = new List<RegionDistribution>
                {
                    new() { Region = "Gauteng", Count = customers.Count(c => c.FirstName.Length % 2 == 0) },
                    new() { Region = "Western Cape", Count = customers.Count(c => c.LastName.Length % 2 == 0) },
                    new() { Region = "KZN", Count = customers.Count(c => c.Email.Contains(".com")) },
                    new() { Region = "Other", Count = customers.Count(c => !c.Email.Contains(".com")) }
                },

                // Real alert severity distribution
                ErrorData = new List<ErrorDistribution>
                {
                    new() { Category = "Critical", Count = alerts.Count(a => a.Severity == "Critical") },
                    new() { Category = "Warning", Count = alerts.Count(a => a.Severity == "Warning") },
                    new() { Category = "Info", Count = alerts.Count(a => a.Severity == "Info") }
                },

                MostActiveRegion = "Gauteng" // Static for now as region isn't a field yet
            };

            return View(model);
        }
        public IActionResult Infrastructure()
        {
            var random = new Random();
            var model = new InfrastructureViewModel
            {
                LastUpdated = DateTime.UtcNow.ToString("HH:mm:ss"),
                CpuUsagePoints = Enumerable.Range(0, 10).Select(_ => random.Next(30, 85)).ToList(),
                MemoryUsagePoints = Enumerable.Range(0, 10).Select(_ => random.Next(40, 90)).ToList(),
                NetworkThroughputPoints = Enumerable.Range(0, 10).Select(_ => random.Next(100, 800)).ToList(),
                Instances = new List<ServerInstance>
                {
                    new() { InstanceId = "srv-app-01", Region = "South Africa North", IpAddress = "10.0.0.4", Status = "Running", Uptime = "45 days" },
                    new() { InstanceId = "srv-app-02", Region = "South Africa West", IpAddress = "10.0.2.8", Status = "Running", Uptime = "12 days" },
                    new() { InstanceId = "srv-db-01", Region = "South Africa North", IpAddress = "10.0.1.15", Status = random.Next(1, 10) > 8 ? "High Load" : "Running", Uptime = "128 days" },
                    new() { InstanceId = "srv-redis-01", Region = "South Africa North", IpAddress = "10.0.1.20", Status = "Running", Uptime = "204 days" }
                }
            };
            return View(model);
        }

        public async Task<IActionResult> Alerts()
        {
            var alerts = await _storageService.GetAlertsAsync();
            return View(alerts);
        }

        [HttpPost]
        public async Task<IActionResult> ResolveAlert(int id)
        {
            await _storageService.UpdateAlertStatusAsync(id, true);
            TempData["Success"] = $"Alert #{id} resolved successfully!";
            return RedirectToAction("Alerts");
        }

        // Mock helpers for visual dashboard
        private List<OrderViewModel> GetMockOrders(int count)
        {
            var orders = new List<OrderViewModel>();
            for (int i = 0; i < count; i++)
            {
                orders.Add(new OrderViewModel
                {
                    OrderId = $"ORD-{10290 + i}",
                    OrderDate = DateTime.UtcNow.AddHours(-i),
                    CustomerName = i % 2 == 0 ? "John Doe" : "Jane Smith",
                    Amount = 49.99m + i * 10,
                    Status = i % 5 == 0 ? "Failed" : (i % 3 == 0 ? "Pending" : "Success")
                });
            }
            return orders;
        }

        private List<AlertViewModel> GetMockAlerts(int count)
        {
            var alerts = new List<AlertViewModel>();
            string[] severities = { "Critical", "Warning", "Info" };
            string[] messages = { "High CPU usage detected", "Queue latency above threshold", "Successful backup", "New storage node added", "API error rate spike" };
            for (int i = 0; i < count; i++)
            {
                alerts.Add(new AlertViewModel { Id = i + 1, Severity = severities[i % 3], Message = messages[i % 5], Timestamp = DateTime.UtcNow.AddMinutes(-i * 15), IsResolved = i > 2 });
            }
            return alerts;
        }
    }
}
