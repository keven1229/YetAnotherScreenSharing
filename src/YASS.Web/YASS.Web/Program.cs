using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using YASS.Web;
using YASS.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// API 服务配置
var apiBaseAddress = builder.Configuration["ApiBaseAddress"] ?? "http://localhost:5000";
builder.Services.AddHttpClient("YassApi", client =>
{
    client.BaseAddress = new Uri(apiBaseAddress);
});

// 应用服务
builder.Services.AddScoped<IRoomApiService, RoomApiService>();
builder.Services.AddScoped<IPlayerService, FlvPlayerService>();

await builder.Build().RunAsync();
