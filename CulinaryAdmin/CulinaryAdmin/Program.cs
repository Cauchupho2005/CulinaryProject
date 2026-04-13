using CulinaryAdmin.Client.Pages;
using CulinaryAdmin.Components;
using MudBlazor.Services;
using Microsoft.AspNetCore.Authentication.Cookies; // Thư viện Cookie

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMudServices();

// Đăng ký HttpClient với địa chỉ API của hệ thống
builder.Services.AddScoped(sp => 
{
    // Nếu đang chạy local, tự động trỏ về localhost:5000
    var apiBaseUrl = builder.Environment.IsDevelopment() 
        ? "http://localhost:5000" 
        : "https://culinary-api-backend.onrender.com";
        
    return new HttpClient
    {
        BaseAddress = new Uri(apiBaseUrl)
    };
});

// ================================================================
// 1. KÍCH HOẠT HỆ THỐNG CẤP COOKIE & API CONTROLLER
// ================================================================
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login"; // Tự động đá về Login nếu chưa có thẻ
    });
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddControllers();
// ================================================================

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

// ================================================================
// 2. BẬT BẢO VỆ VÀ MỞ CỬA CHO PHÒNG CẤP THẺ HOẠT ĐỘNG
// ================================================================

app.MapControllers();
// ================================================================

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(CulinaryAdmin.Client._Imports).Assembly);

app.Run();