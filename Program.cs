using KampusImece.Data;
using KampusImece.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

// PostgreSQL Tarih/Saat Uyumluluğu
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Render / Bulut Sunucu Dinamik Port Yapılandırması (IPv4 0.0.0.0 Garantili)
var portStr = Environment.GetEnvironmentVariable("PORT");
int listenPort = 10000;
if (int.TryParse(portStr, out var parsedPort))
{
    listenPort = parsedPort;
}

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Listen(System.Net.IPAddress.Any, listenPort);
});

// 1. Controller & View Servisleri
builder.Services.AddControllersWithViews();

// 2. EF Core PostgreSQL Veritabanı Servisi (Cloud URI & Standart Bağlantı Desteği)
var rawConnectionString = Environment.GetEnvironmentVariable("DATABASE_URL") 
    ?? builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Host=localhost;Port=5432;Database=kampusimece_db;Username=yes;Password=";

var connectionString = ConvertPostgresUriToConnectionString(rawConnectionString);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// 3. Güvenlik & İş Katmanı Bağımlılık Kayıtları (Dependency Injection)
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<IEmailService, EmailService>();

// 4. Antiforgery (CSRF Koruması) Yapılandırması
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "KampusImece.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.HeaderName = "RequestVerificationToken";
});

// 5. Cookie-Based Authentication Yapılandırması
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "KampusImece.AuthTicket";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
    });

// 6. Yetkilendirme (Authorization) Politikaları
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CoordinatorOnly", policy => policy.RequireRole("Coordinator"));
});

var app = builder.Build();

// 7. Güvenli Yükleme Klasörünün (wwwroot/uploads/spots) Varlığını Garanti Altına Al
var webRoot = app.Environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
var uploadsSpotsFolder = Path.Combine(webRoot, "uploads", "spots");
if (!Directory.Exists(uploadsSpotsFolder))
{
    Directory.CreateDirectory(uploadsSpotsFolder);
}

// 8. Veritabanı Otomatik Migration & Seed (DbSeeder - Render Port Taramasını Bekletmeyen Asenkron Başlatma)
app.Lifetime.ApplicationStarted.Register(async () =>
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        var passwordHasher = services.GetRequiredService<IPasswordHasher>();
        await DbSeeder.SeedAsync(context, passwordHasher);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Veritabanı migration veya tohumlama (Seed) işlemi sırasında bir hata oluştu.");
    }
});

// 9. HTTP İşlem Hattı (Middleware Pipeline)
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Events/Error");
    app.UseHsts();
}

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // JS ve CSS dosyalarında tarayıcı önbelleklemesini önleyerek güncellemelerin anında görünmesini sağla
        if (ctx.File.Name.EndsWith(".js", StringComparison.OrdinalIgnoreCase) || 
            ctx.File.Name.EndsWith(".css", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
            ctx.Context.Response.Headers.Append("Pragma", "no-cache");
            ctx.Context.Response.Headers.Append("Expires", "0");
        }
    }
});

app.UseRouting();

// Authentication her zaman Authorization'dan önce çalışmalıdır
app.UseAuthentication();
app.UseAuthorization();

// Varsayılan Rota: Events/Index
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Events}/{action=Index}/{id?}");

app.Run();

// Bulut Sağlayıcılar (Render, Railway, Heroku, Supabase) için PostgreSQL URI Dönüştürücü
static string ConvertPostgresUriToConnectionString(string rawConn)
{
    if (string.IsNullOrWhiteSpace(rawConn)) return rawConn;

    if (rawConn.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) || 
        rawConn.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        try
        {
            var uri = new Uri(rawConn);
            var userInfo = uri.UserInfo.Split(':');
            var username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "";
            var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
            var database = uri.AbsolutePath.TrimStart('/');
            var port = uri.Port > 0 ? uri.Port : 5432;

            return $"Host={uri.Host};Port={port};Database={database};Username={username};Password={password};Ssl Mode=Prefer;Trust Server Certificate=true";
        }
        catch
        {
            return rawConn;
        }
    }

    return rawConn;
}
