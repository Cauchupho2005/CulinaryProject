using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using System.Net.Http; // <--- Thêm thư viện này để hết lỗi HttpClient
using System;          // <--- Thêm thư viện này để hết lỗi Uri

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Dạy cho trình duyệt biết Backend thật sự nằm ở đâu
builder.Services.AddScoped(sp =>
{
    var apiBaseUrl = builder.HostEnvironment.IsDevelopment()
        ? "http://localhost:5000"
        : "https://culinary-api-backend.onrender.com";

    return new HttpClient
    {
        BaseAddress = new Uri(apiBaseUrl)
    };
});

await builder.Build().RunAsync();