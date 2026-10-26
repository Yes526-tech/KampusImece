using System.Security.Claims;
using KampusImece.Data;
using KampusImece.Models.Entities;
using KampusImece.Models.ViewModels;
using KampusImece.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KampusImece.Controllers;

public class AuthController : Controller
{
    private readonly AppDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        AppDbContext context,
        IPasswordHasher passwordHasher,
        ILogger<AuthController> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocal(returnUrl);
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);

        if (user == null || !_passwordHasher.VerifyPassword(model.Password, user.PasswordHash))
        {
            ModelState.AddModelError(string.Empty, "Geçersiz e-posta adresi veya şifre girdiniz.");
            return View(model);
        }

        // Kullanıcı Claim'lerini oluştur
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.IsCoordinator ? "Coordinator" : "Student"),
            new("StudentNumber", user.StudentNumber),
            new("ReliabilityScore", user.ReliabilityScore.ToString()),
            new("IsCoordinator", user.IsCoordinator ? "true" : "false")
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = model.RememberMe,
            ExpiresUtc = model.RememberMe 
                ? DateTimeOffset.UtcNow.AddDays(14) 
                : DateTimeOffset.UtcNow.AddHours(8)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);

        _logger.LogInformation("Kullanıcı başarıyla giriş yaptı: {Email}", user.Email);

        TempData["SuccessMessage"] = $"Hoş geldiniz, {user.FullName}!";
        return RedirectToLocal(model.ReturnUrl);
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Events");
        }

        return View(new RegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!model.AcceptKvkk)
        {
            ModelState.AddModelError("AcceptKvkk", "Kayıt olabilmek için KVKK Aydınlatma Metni ve Açık Rıza Beyanını onaylamanız zorunludur.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();

        // E-posta benzersizlik kontrolü
        if (await _context.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail))
        {
            ModelState.AddModelError("Email", "Bu e-posta adresi ile kayıtlı bir hesap zaten bulunmaktadır.");
            return View(model);
        }

        // Öğrenci numarası benzersizlik kontrolü
        var normalizedStudentNo = model.StudentNumber.Trim();
        if (await _context.Users.AnyAsync(u => u.StudentNumber == normalizedStudentNo))
        {
            ModelState.AddModelError("StudentNumber", "Bu öğrenci numarası ile kayıtlı bir hesap zaten bulunmaktadır.");
            return View(model);
        }

        // PBKDF2 Şifre Hashleme
        var hashedPassword = _passwordHasher.HashPassword(model.Password);

        var newUserId = Guid.NewGuid();
        var approvedAt = DateTime.UtcNow;
        var kvkkVersion = "v1.0";
        var auditHash = _passwordHasher.ComputeKvkkAuditHash(newUserId, normalizedEmail, normalizedStudentNo, approvedAt, kvkkVersion);

        var isCoordinator = normalizedEmail == "ysert20051@gmail.com" || 
                            normalizedEmail == "233311021@ogr.selcuk.edu.tr" || 
                            normalizedEmail == "233311070@ogr.selcuk.edu.tr";

        var newUser = new AppUser
        {
            Id = newUserId,
            FullName = model.FullName.Trim(),
            Email = normalizedEmail,
            StudentNumber = normalizedStudentNo,
            PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim(),
            PasswordHash = hashedPassword,
            ReliabilityScore = 100,
            IsCoordinator = isCoordinator,
            CreatedAt = approvedAt,
            IsKvkkApproved = true,
            KvkkApprovedAt = approvedAt,
            KvkkVersion = kvkkVersion,
            KvkkConsentHash = auditHash
        };

        _context.Users.Add(newUser);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Yeni öğrenci kaydı oluşturuldu: {Email} ({StudentNo})", newUser.Email, newUser.StudentNumber);

        // Kayıt sonrası otomatik oturum açma
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, newUser.Id.ToString()),
            new(ClaimTypes.Name, newUser.FullName),
            new(ClaimTypes.Email, newUser.Email),
            new(ClaimTypes.Role, newUser.IsCoordinator ? "Coordinator" : "Student"),
            new("StudentNumber", newUser.StudentNumber),
            new("ReliabilityScore", newUser.ReliabilityScore.ToString()),
            new("IsCoordinator", newUser.IsCoordinator ? "true" : "false")
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            new AuthenticationProperties { IsPersistent = false, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8) });

        TempData["SuccessMessage"] = "Hesabınız başarıyla oluşturuldu! Kampüs İmece'ye hoş geldiniz.";
        return RedirectToAction("Index", "Events");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        _logger.LogInformation("Kullanıcı oturumu sonlandırıldı.");
        TempData["SuccessMessage"] = "Oturumunuz güvenli bir şekilde kapatıldı.";
        return RedirectToAction("Index", "Events");
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Events");
    }
}
