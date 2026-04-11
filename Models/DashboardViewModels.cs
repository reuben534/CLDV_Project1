using System;

namespace ABC_Retail.Models
{
    public class OrderViewModel
    {
        public string OrderId { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = "Pending"; // Pending, Success, Failed
    }

    public class QueueMessageViewModel
    {
        public string MessageId { get; set; } = string.Empty;
        public string PopReceipt { get; set; } = string.Empty;
        public string QueueName { get; set; } = string.Empty;
        public DateTime EnqueuedTime { get; set; }
        public string Payload { get; set; } = string.Empty;
        public int RetryCount { get; set; }
    }

    public class AlertViewModel
    {
        public int Id { get; set; }
        public string Severity { get; set; } = "Info"; // Info, Warning, Critical
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public bool IsResolved { get; set; }
    }

    public class DashboardSummaryViewModel
    {
        public int ActiveOrders { get; set; }
        public int QueueDepth { get; set; }
        public double SystemUptime { get; set; }
        public double ErrorRate { get; set; }
        public List<OrderViewModel> RecentOrders { get; set; } = new();
        public List<AlertViewModel> RecentAlerts { get; set; } = new();
    }

    public class CustomerProfile
    {
        public string PartitionKey { get; set; } = "Customers";
        public string RowKey { get; set; } = Guid.NewGuid().ToString();
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
    }

    public class ProductImage
    {
        public string ImageName { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public DateTime UploadDate { get; set; }
        public long FileSize { get; set; }
    }

    public class AnalyticsViewModel
    {
        public List<DailyOrderCount> DailyOrders { get; set; } = new();
        public List<RegionDistribution> RegionData { get; set; } = new();
        public List<ErrorDistribution> ErrorData { get; set; } = new();
        public decimal TotalRevenue { get; set; }
        public int TotalCustomers { get; set; }
        public string MostActiveRegion { get; set; } = "N/A";
        public double AverageOrderValue { get; set; }
    }

    public class DailyOrderCount
    {
        public string Day { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class RegionDistribution
    {
        public string Region { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class ErrorDistribution
    {
        public string Category { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class InfrastructureViewModel
    {
        public List<ServerInstance> Instances { get; set; } = new();
        public List<int> CpuUsagePoints { get; set; } = new();
        public List<int> MemoryUsagePoints { get; set; } = new();
        public List<int> NetworkThroughputPoints { get; set; } = new();
        public string LastUpdated { get; set; } = string.Empty;
    }

    public class ServerInstance
    {
        public string InstanceId { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // Running, High Load, Down
        public string Uptime { get; set; } = string.Empty;
    }

    public class AdminUser
    {
        public string PartitionKey { get; set; } = "Staff";
        public string RowKey { get; set; } = string.Empty; // This will be the Username
        public string PasswordHash { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
    }
}
