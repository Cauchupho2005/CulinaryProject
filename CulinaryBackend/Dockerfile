# 1. Dùng .NET 10 SDK để Build ứng dụng
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy file dự án và khôi phục các gói (restore)
COPY ["CulinaryBackend.csproj", "./"]
RUN dotnet restore "./CulinaryBackend.csproj"

# Copy toàn bộ code và xuất bản (publish)
COPY . .
RUN dotnet publish "CulinaryBackend.csproj" -c Release -o /app/publish

# 2. Dùng .NET 10 ASP.NET để Chạy ứng dụng (Nhẹ hơn)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Lệnh khởi chạy ứng dụng
ENTRYPOINT ["dotnet", "CulinaryBackend.dll"]