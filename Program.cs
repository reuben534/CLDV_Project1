using Microsoft.Extensions.Azure;
var builder = WebApplication.CreateBuilder(args);

// Set culture to South Africa for Rand currency formatting
var cultureInfo = new System.Globalization.CultureInfo("en-ZA");
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<ABC_Retail.Services.AzureStorageService>();
builder.Services.AddAzureClients(clientBuilder =>
{
    clientBuilder.AddBlobServiceClient(builder.Configuration["AzureStorage:ConnectionString1:blobServiceUri"]!).WithName("AzureStorage:ConnectionString1");
    clientBuilder.AddQueueServiceClient(builder.Configuration["AzureStorage:ConnectionString1:queueServiceUri"]!).WithName("AzureStorage:ConnectionString1");
    clientBuilder.AddTableServiceClient(builder.Configuration["AzureStorage:ConnectionString1:tableServiceUri"]!).WithName("AzureStorage:ConnectionString1");
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();
