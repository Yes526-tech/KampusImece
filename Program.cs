using KampusImece.Data;
using KampusImece.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

// PostgreSQL Tarih/Saat Uyumluluğu
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// 1. Controller & View Servisleri
builder.Services.AddControllersWithViews();

// 2. EF Core PostgreSQL Veritabanı Servisi
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection") 
                      ?? "Host=localhost;Port=5432;Database=kampusimece_db;Username=yes;Password="));

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
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.HeaderName = "RequestVerificationToken";
});

// 5. Cookie-Based Authentication Yapılandırması
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "KampusImece.AuthTicket";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
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

// 8. Veritabanı Otomatik Migration & Seed (DbSeeder)
using (var scope = app.Services.CreateScope())
{
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
}

// 9. HTTP İşlem Hattı (Middleware Pipeline)
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Events/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Authentication her zaman Authorization'dan önce çalışmalıdır
app.UseAuthentication();
app.UseAuthorization();

// Varsayılan Rota: Events/Index
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Events}/{action=Index}/{id?}");

app.Run();
