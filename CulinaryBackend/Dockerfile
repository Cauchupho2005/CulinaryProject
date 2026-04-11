# Sử dụng .NET 10 SDK
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Chỉ cho Docker biết file .csproj nằm ở trong thư mục con
COPY ["CulinaryBackend/CulinaryBackend.csproj", "CulinaryBackend/"]
RUN dotnet restore "CulinaryBackend/CulinaryBackend.csproj"

# Copy toàn bộ code còn lại vào
COPY . .

# Chui vào thư mục con đó để Build
WORKDIR "/src/CulinaryBackend"
RUN dotnet publish "CulinaryBackend.csproj" -c Release -o /app/publish

# Giai đoạn chạy (Runtime)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CulinaryBackend.dll"]