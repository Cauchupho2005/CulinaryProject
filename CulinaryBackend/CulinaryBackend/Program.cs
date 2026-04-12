using MongoDB.Driver;

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

// =================================================================
// 2. PHẦN KHẮC PHỤC LỖI 500: ĐĂNG KÝ KẾT NỐI MONGODB

var connectionString = builder.Configuration.GetConnectionString("MongoDb") ?? "mongodb+srv://VinhKhanhTour:VinhKhanhTour123%40@cluster0.yzzftyt.mongodb.net/?appName=Cluster0";
var mongoClient = new MongoClient(connectionString);

// Thay "CulinaryDB" bằng tên Database thật của bạn trên MongoDB Atlas
var database = mongoClient.GetDatabase("CulinaryDB");

// Bơm Database vào hệ thống để AuthController xài được
builder.Services.AddSingleton<IMongoDatabase>(database);
// =================================================================

builder.Services.AddSingleton<CulinaryBackend.Services.PoiService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Bật Swagger để bạn test trên máy tính
app.UseSwagger();
app.UseSwaggerUI();

// 3. Kích hoạt chính sách CORS
app.UseCors();

app.UseAuthorization();
app.MapControllers();

// 4. QUAN TRỌNG NHẤT: Ép API lắng nghe trên mọi địa chỉ IP qua cổng 5000
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
app.Run($"http://0.0.0.0:{port}");