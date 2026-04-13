# Sử dụng image SDK để build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy các file dự án để khôi phục thư viện (Blazor có 2 cục Server và Client)
COPY ["CulinaryAdmin/CulinaryAdmin/CulinaryAdmin.csproj", "CulinaryAdmin/CulinaryAdmin/"]
COPY ["CulinaryAdmin/CulinaryAdmin.Client/CulinaryAdmin.Client.csproj", "CulinaryAdmin/CulinaryAdmin.Client/"]
RUN dotnet restore "CulinaryAdmin/CulinaryAdmin/CulinaryAdmin.csproj"

# Copy toàn bộ code còn lại vào và Build
COPY . .
WORKDIR "/src/CulinaryAdmin/CulinaryAdmin"
RUN dotnet build "CulinaryAdmin.csproj" -c Release -o /app/build

# Publish (Đóng gói) dự án
FROM build AS publish
RUN dotnet publish "CulinaryAdmin.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Sử dụng image Runtime nhẹ để chạy web
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CulinaryAdmin.dll"]