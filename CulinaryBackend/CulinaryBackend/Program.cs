var builder = WebApplication.CreateBuilder(args);

// 1. Cấu hình CORS: Cho phép điện thoại truy cập vào API
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddSingleton<CulinaryBackend.Services.PoiService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Bật Swagger để bạn test trên máy tính
app.UseSwagger();
app.UseSwaggerUI();

// 2. QUAN TRỌNG: XÓA HOẶC COMMENT DÒNG HTTPS DƯỚI ĐÂY
// app.UseHttpsRedirection(); 

// 3. Kích hoạt chính sách CORS
app.UseCors();

app.UseAuthorization();
app.MapControllers();

// 4. QUAN TRỌNG NHẤT: Ép API lắng nghe trên mọi địa chỉ IP qua cổng 5000
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
app.Run($"http://0.0.0.0:{port}");