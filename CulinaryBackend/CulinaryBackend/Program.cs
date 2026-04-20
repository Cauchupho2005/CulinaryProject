using MongoDB.Driver;
using CulinaryBackend.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Cấu hình CORS: Cho phép Admin và App gọi vào API từ bất cứ đâu
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// 2. KẾT NỐI MONGODB: Đọc thông số từ mục "MongoDbSettings" trong appsettings.json
var mongoSettings = builder.Configuration.GetSection("MongoDbSettings");
var connectionString = mongoSettings["ConnectionString"];
var databaseName = mongoSettings["DatabaseName"];

// Khởi tạo MongoClient và Database
var mongoClient = new MongoClient(connectionString);
var database = mongoClient.GetDatabase(databaseName);

// Đăng ký IMongoDatabase vào hệ thống (Dependency Injection)
builder.Services.AddSingleton<IMongoDatabase>(database);

// 3. ĐĂNG KÝ CÁC SERVICE (Để logic chạy được)
builder.Services.AddSingleton<PoiService>();
builder.Services.AddSingleton<UserLogService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddSingleton<PoiVisitService>();
// Đăng ký IMemoryCache mặc định của .NET
builder.Services.AddMemoryCache();

// Đăng ký ActiveUserTracker dưới dạng Singleton (chỉ tạo 1 bản duy nhất trên server)
builder.Services.AddSingleton<ActiveUserTracker>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// 4. CẤU HÌNH PIPELINE (Thứ tự chạy của web)
// Bật Swagger ở mọi môi trường để bạn dễ dàng test link Render
app.UseSwagger();
app.UseSwaggerUI();

// Kích hoạt CORS ngay trước Authorization
app.UseCors();

app.UseAuthorization();
app.MapControllers();
app.UseStaticFiles();

// 5. CẤU HÌNH CỔNG (PORT) CHO RENDER
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
app.Run($"http://0.0.0.0:{port}");