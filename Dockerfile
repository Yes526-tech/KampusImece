# Build Aşaması
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["KampusImece.csproj", "./"]
RUN dotnet restore "KampusImece.csproj"

COPY . .
RUN dotnet publish "KampusImece.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime (Çalışma) Aşaması
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 10000
ENV ASPNETCORE_ENVIRONMENT=Production
ENV PORT=10000
ENV ASPNETCORE_HTTP_PORTS=10000
ENV ASPNETCORE_URLS=http://0.0.0.0:10000

COPY --from=build /app/publish .

# Görsel yükleme klasörü ve izinleri
RUN mkdir -p /app/wwwroot/uploads/spots && chmod -R 777 /app/wwwroot/uploads

ENTRYPOINT ["dotnet", "KampusImece.dll"]
