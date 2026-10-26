using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using KampusImece.Data;
using KampusImece.Models;
using KampusImece.Models.Entities;
using KampusImece.Models.ViewModels;
using KampusImece.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KampusImece.Controllers;

public class EventsController : Controller
{
    private readonly AppDbContext _context;
    private readonly IFileStorageService _fileStorageService;
    private readonly ILogger<EventsController> _logger;

    // Eşzamanlı son kota ve kayıt yarış durumlarını (Race Condition) engellemek için işlem kilidi
    private static readonly SemaphoreSlim _concurrencyLock = new(1, 1);

    public EventsController(
        AppDbContext context,
        IFileStorageService fileStorageService,
        ILogger<EventsController> logger)
    {
        _context = context;
        _fileStorageService = fileStorageService;
        _logger = logger;
    }

    /// <summary>
    /// Ana sayfa harita ve kirlilik noktası listesini içeren arayüz
    /// </summary>
    [HttpGet]
    [Route("/")]
    [Route("Events")]
    [Route("Events/Index")]
    public async Task<IActionResult> Index()
    {
        // Özet istatistikler
        ViewBag.TotalEvents = await _context.CleanupEvents.CountAsync();
        ViewBag.OpenEvents = await _context.CleanupEvents.CountAsync(e => e.Status == EventStatus.Open);
        ViewBag.TotalVolunteers = await _context.EventParticipations.Select(p => p.UserId).Distinct().CountAsync();
        ViewBag.CompletedCleanups = await _context.CleanupEvents.CountAsync(e => e.Status == EventStatus.Completed);

        return View();
    }

    /// <summary>
    /// Kirlilik ilanının tüm detaylarını, fotoğraflarını, katılımcı listesini ve harita konumunu içeren detay sayfası
    /// </summary>
    [HttpGet]
    [Route("Events/Details/{id:guid}")]
    public async Task<IActionResult> Details(Guid id)
    {
        var ev = await _context.CleanupEvents
            .Include(e => e.Creator)
            .Include(e => e.Participations)
                .ThenInclude(p => p.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id);

        if (ev == null)
        {
            return NotFound();
        }

        var currentUserId = GetCurrentUserId();
        var hasJoined = currentUserId.HasValue && ev.Participations.Any(p => p.UserId == currentUserId.Value);
        var isCreator = currentUserId.HasValue && ev.CreatorId == currentUserId.Value;
        var canComplete = currentUserId.HasValue &&
                          ev.Status != EventStatus.Cancelled &&
                          (isCreator || hasJoined);

        ViewBag.CurrentUserId = currentUserId;
        ViewBag.HasJoined = hasJoined;
        ViewBag.IsCreator = isCreator;
        ViewBag.CanComplete = canComplete;

        return View(ev);
    }

    /// <summary>
    /// Kullanıcının oluşturduğu kirlilik ilanlarının ve temizlik geçmişinin yer aldığı geçmiş sayfası
    /// </summary>
    [HttpGet]
    [Authorize]
    [Route("Events/History")]
    [Route("Events/MyHistory")]
    public async Task<IActionResult> History()
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return RedirectToAction("Login", "Auth", new { returnUrl = "/Events/History" });
        }

        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == currentUserId.Value);
        if (user == null)
        {
            return RedirectToAction("Login", "Auth");
        }

        // Kullanıcının bizzat açtığı tüm ilanlar (aktifler üstte, tamamlananlar altta; tarihe göre sıralı)
        var myCreatedEvents = await _context.CleanupEvents
            .Include(e => e.Creator)
            .Include(e => e.Participations)
                .ThenInclude(p => p.User)
            .Where(e => e.CreatorId == currentUserId.Value && e.Status != EventStatus.Cancelled)
            .OrderBy(e => e.Status == EventStatus.Completed ? 1 : 0)
            .ThenBy(e => e.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        // Kullanıcının katıldığı diğer temizlikler
        var myParticipations = await _context.CleanupEvents
            .Include(e => e.Creator)
            .Include(e => e.Participations)
                .ThenInclude(p => p.User)
            .Where(e => e.CreatorId != currentUserId.Value && 
                        e.Participations.Any(p => p.UserId == currentUserId.Value) && 
                        e.Status != EventStatus.Cancelled)
            .OrderBy(e => e.Status == EventStatus.Completed ? 1 : 0)
            .ThenBy(e => e.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        var createdCount = myCreatedEvents.Count;
        var completedCount = myCreatedEvents.Count(e => e.Status == EventStatus.Completed) +
                             myParticipations.Count(e => e.Status == EventStatus.Completed);
        var activeCount = myCreatedEvents.Count(e => e.Status == EventStatus.Open || e.Status == EventStatus.QuotaFull);
        var participationsCount = myParticipations.Count;

        var totalScore = (createdCount * 25) + (participationsCount * 30) + (completedCount * 50);

        var model = new UserHistoryViewModel
        {
            UserFullName = user.FullName,
            UserEmail = user.Email,
            Initials = GetInitials(user.FullName),
            CreatedEventsCount = createdCount,
            CompletedCleanupsCount = completedCount,
            ActiveEventsCount = activeCount,
            TotalParticipationsCount = participationsCount,
            TotalScore = totalScore,
            BadgeTitle = LeaderboardEntryDto.DetermineBadge(totalScore),
            MyCreatedEvents = myCreatedEvents,
            MyParticipations = myParticipations
        };

        return View(model);
    }

    /// <summary>
    /// Harita ve kart listesi için tüm aktif ve tamamlanmış etkinlikleri JSON olarak döner (filtreleme destekli)
    /// </summary>
    [HttpGet]
    [Route("api/events")]
    public async Task<IActionResult> GetEvents(
        [FromQuery] string? status = null,
        [FromQuery] string? search = null)
    {
        var currentUserId = GetCurrentUserId();

        var query = _context.CleanupEvents
            .Include(e => e.Creator)
            .Include(e => e.Participations)
                .ThenInclude(p => p.User)
            .Where(e => e.Status != EventStatus.Cancelled)
            .AsNoTracking()
            .AsQueryable();

        // 1. Arama Filtresi (Başlık veya Konum Adı)
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(e => e.Title.ToLower().Contains(term) || 
                                     e.LocationName.ToLower().Contains(term) ||
                                     e.Description.ToLower().Contains(term));
        }

        // 2. Durum / Sekme Filtresi
        if (!string.IsNullOrWhiteSpace(status))
        {
            switch (status.ToLower())
            {
                case "open":
                    query = query.Where(e => e.Status == EventStatus.Open);
                    break;
                case "quotafull":
                    query = query.Where(e => e.Status == EventStatus.QuotaFull);
                    break;
                case "completed":
                    query = query.Where(e => e.Status == EventStatus.Completed);
                    break;
                case "my-events":
                    if (currentUserId.HasValue)
                    {
                        query = query.Where(e => e.CreatorId == currentUserId.Value);
                    }
                    break;
                case "my-participations":
                    if (currentUserId.HasValue)
                    {
                        query = query.Where(e => e.Participations.Any(p => p.UserId == currentUserId.Value));
                    }
                    break;
            }
        }

        // Temizlenen yerler her zaman en altta sabit, aktif ilanlar ise tarihe göre (en son açılan en sonda)
        var events = await query
            .OrderBy(e => e.Status == EventStatus.Completed ? 1 : 0)
            .ThenBy(e => e.CreatedAt)
            .ToListAsync();

        var dtoList = events.Select(e =>
        {
            var participantsCount = e.Participations.Count;
            var isQuotaFull = participantsCount >= e.RequiredVolunteers || e.Status == EventStatus.QuotaFull || e.Status == EventStatus.Completed;
            var hasJoined = currentUserId.HasValue && e.Participations.Any(p => p.UserId == currentUserId.Value);
            var isCreator = currentUserId.HasValue && e.CreatorId == currentUserId.Value;

            // Kullanıcı bu ilanın kurucusu VEYA katılımcısıysa ve ilan henüz tamamlanmamışsa tamamlayabilir
            var canComplete = currentUserId.HasValue && 
                              e.Status != EventStatus.Completed && 
                              e.Status != EventStatus.Cancelled &&
                              (isCreator || hasJoined);

            return new EventCardDto
            {
                Id = e.Id,
                Title = e.Title,
                Description = e.Description,
                LocationName = e.LocationName,
                Latitude = e.Latitude,
                Longitude = e.Longitude,
                ImageUrl = e.ImageUrl,
                AfterImageUrl = e.AfterImageUrl,
                ScheduledAt = e.ScheduledAt,
                CompletedAt = e.CompletedAt,
                CompletedByUserId = e.CompletedByUserId,
                CompletionNote = e.CompletionNote,
                Status = e.Status,
                RequiredVolunteers = e.RequiredVolunteers,
                CurrentParticipantsCount = participantsCount,
                IsQuotaFull = isQuotaFull,
                HasCurrentUserJoined = hasJoined,
                IsCreator = isCreator,
                CanCurrentUserComplete = canComplete,
                CreatorName = e.Creator.FullName,
                CreatedAt = e.CreatedAt,
                ParticipantsList = e.Participations
                    .OrderBy(p => p.JoinedAt)
                    .Select(p => new ParticipantSummaryDto
                    {
                        UserId = p.UserId,
                        FullName = p.User.FullName,
                        Initials = GetInitials(p.User.FullName),
                        JoinedAt = p.JoinedAt
                    }).ToList()
            };
        }).ToList();

        return Ok(dtoList);
    }

    /// <summary>
    /// Yeni kirlilik noktası ve fotoğraf yükler; oluşturan kişiyi otomatik ilk gönüllü (1/N) olarak kaydeder!
    /// InvariantCulture ile enlem/boylam noktadan sonraki basamakları hatasız parse eder.
    /// </summary>
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    [Route("api/events")]
    public async Task<IActionResult> CreateEvent([FromForm] CreateEventViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(new { success = false, message = "Form doğrulama hatası.", errors });
        }

        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return Unauthorized(new { success = false, message = "Oturum açmanız gerekmektedir." });
        }

        // 1. Güvenli Fotoğraf Yükleme Servisi (Öncesi Fotoğrafı)
        string imagePath;
        try
        {
            imagePath = await _fileStorageService.SaveSpotImageAsync(model.Photo);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fotoğraf yüklenirken güvenlik/doğrulama hatası.");
            return BadRequest(new { success = false, message = ex.Message });
        }

        // İsteğe Bağlı: Temizlik Sonrası Fotoğrafı (İlanı oluşturan doğrudan eklediyse)
        string? afterImagePath = null;
        if (model.AfterPhoto != null && model.AfterPhoto.Length > 0)
        {
            try
            {
                afterImagePath = await _fileStorageService.SaveSpotImageAsync(model.AfterPhoto);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Sonrası fotoğrafı yüklenirken hata.");
                return BadRequest(new { success = false, message = "Sonrası fotoğrafı hatası: " + ex.Message });
            }
        }

        // 2. Kültür (Culture) Bağımsız InvariantCulture Koordinat Çözümleme
        double parsedLatitude = model.ParsedLatitude;
        double parsedLongitude = model.ParsedLongitude;

        if (parsedLatitude == 0 || parsedLongitude == 0)
        {
            parsedLatitude = CreateEventViewModel.ParseCoordinate(model.Latitude, 38.0260);
            parsedLongitude = CreateEventViewModel.ParseCoordinate(model.Longitude, 32.5090);
        }

        // 3. Transaction ve Concurrency Koruması ile Etkinlik ve İlk Katılım Kaydı
        await _concurrencyLock.WaitAsync();
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var eventId = Guid.NewGuid();
            var hasAfterPhoto = !string.IsNullOrEmpty(afterImagePath);
            var initialStatus = hasAfterPhoto 
                ? EventStatus.Completed 
                : (model.RequiredVolunteers <= 1 ? EventStatus.QuotaFull : EventStatus.Open);

            var newEvent = new CleanupEvent
            {
                Id = eventId,
                Title = model.Title.Trim(),
                Description = model.Description.Trim(),
                LocationName = model.LocationName.Trim(),
                Latitude = parsedLatitude,
                Longitude = parsedLongitude,
                RequiredVolunteers = model.RequiredVolunteers,
                ScheduledAt = model.ScheduledAt,
                ImageUrl = imagePath,
                AfterImageUrl = afterImagePath,
                CompletedAt = hasAfterPhoto ? DateTime.UtcNow : null,
                CompletedByUserId = hasAfterPhoto ? currentUserId.Value : null,
                CompletionNote = hasAfterPhoto ? "İlan oluşturulurken temizlik öncesi ve sonrası fotoğrafları birlikte yüklendi." : null,
                Status = initialStatus,
                IsApproved = true,
                CreatorId = currentUserId.Value,
                CreatedAt = DateTime.UtcNow
            };

            // Etkinliği veritabanına ekle
            _context.CleanupEvents.Add(newEvent);

            // ÖNEMLİ KURAL: Oluşturan kişiyi otomatik ilk gönüllü (1/N) olarak kaydeder!
            var creatorParticipation = new EventParticipation
            {
                Id = Guid.NewGuid(),
                EventId = eventId,
                UserId = currentUserId.Value,
                JoinedAt = DateTime.UtcNow
            };

            _context.EventParticipations.Add(creatorParticipation);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Yeni kirlilik etkinliği ve ilk katılım oluşturuldu: {Title} by User: {UserId} [Lat: {Lat}, Lng: {Lng}]", 
                newEvent.Title, currentUserId.Value, newEvent.Latitude, newEvent.Longitude);

            var creator = await _context.Users.FindAsync(currentUserId.Value);

            var responseDto = new EventCardDto
            {
                Id = newEvent.Id,
                Title = newEvent.Title,
                Description = newEvent.Description,
                LocationName = newEvent.LocationName,
                Latitude = newEvent.Latitude,
                Longitude = newEvent.Longitude,
                ImageUrl = newEvent.ImageUrl,
                AfterImageUrl = newEvent.AfterImageUrl,
                ScheduledAt = newEvent.ScheduledAt,
                CompletedAt = newEvent.CompletedAt,
                CompletedByUserId = newEvent.CompletedByUserId,
                CompletionNote = newEvent.CompletionNote,
                Status = newEvent.Status,
                RequiredVolunteers = newEvent.RequiredVolunteers,
                CurrentParticipantsCount = 1,
                IsQuotaFull = (newEvent.Status == EventStatus.Completed || newEvent.RequiredVolunteers <= 1),
                HasCurrentUserJoined = true,
                IsCreator = true,
                CanCurrentUserComplete = !hasAfterPhoto,
                CreatorName = creator?.FullName ?? "Bilinmiyor",
                CreatedAt = newEvent.CreatedAt,
                ParticipantsList = new List<ParticipantSummaryDto>
                {
                    new()
                    {
                        UserId = currentUserId.Value,
                        FullName = creator?.FullName ?? "",
                        Initials = GetInitials(creator?.FullName ?? "Ö"),
                        JoinedAt = creatorParticipation.JoinedAt
                    }
                }
            };

            return StatusCode(StatusCodes.Status201Created, new
            {
                success = true,
                message = "Kirlilik noktası bildirildi ve ilk gönüllü olarak kaydınız yapıldı!",
                data = responseDto,
                id = responseDto.Id,
                title = responseDto.Title,
                latitude = responseDto.Latitude,
                longitude = responseDto.Longitude,
                imageUrl = responseDto.ImageUrl,
                locationName = responseDto.LocationName,
                requiredVolunteers = responseDto.RequiredVolunteers,
                currentParticipantsCount = responseDto.CurrentParticipantsCount,
                isCreator = true
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Kirlilik noktası kaydedilirken hata oluştu.");
            await _fileStorageService.DeleteImageAsync(imagePath);
            return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = "Kayıt işlemi sırasında bir sunucu hatası oluştu." });
        }
        finally
        {
            _concurrencyLock.Release();
        }
    }

    /// <summary>
    /// Gönüllü Katılım / İptal (Toggle Join) İş Mantığı:
    /// Semaphore + Serializable Transaction kilidi kullanır.
    /// </summary>
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    [Route("api/events/{id:guid}/toggle-join")]
    public async Task<IActionResult> ToggleJoin(Guid id)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return Unauthorized(new { success = false, message = "Katılım için oturum açmalısınız." });
        }

        // Concurrency Lock: Aynı anda gelen istekler için thread ve veritabanı kilit koruması
        await _concurrencyLock.WaitAsync();
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var targetEvent = await _context.CleanupEvents
                .Include(e => e.Participations)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (targetEvent == null)
            {
                return NotFound(new { success = false, message = "Etkinlik bulunamadı." });
            }

            if (targetEvent.Status == EventStatus.Completed || targetEvent.Status == EventStatus.Cancelled)
            {
                return BadRequest(new { success = false, message = "Bu etkinlik tamamlanmış veya iptal edilmiş olduğundan katılım değiştirilemez." });
            }

            var existingParticipation = targetEvent.Participations
                .FirstOrDefault(p => p.UserId == currentUserId.Value);

            string actionType;
            string message;

            if (existingParticipation != null)
            {
                // KULLANICI AYRILIYOR (LEAVE)
                _context.EventParticipations.Remove(existingParticipation);
                targetEvent.Participations.Remove(existingParticipation);

                if (targetEvent.Status == EventStatus.QuotaFull)
                {
                    targetEvent.Status = EventStatus.Open;
                }

                actionType = "left";
                message = "Etkinlik katılımınız iptal edildi.";
            }
            else
            {
                // KULLANICI KATILIYOR (JOIN)
                if (targetEvent.Participations.Count >= targetEvent.RequiredVolunteers)
                {
                    targetEvent.Status = EventStatus.QuotaFull;
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return BadRequest(new { success = false, message = "Üzgünüz! Etkinlik kontenjanı dolmuştur." });
                }

                var newParticipation = new EventParticipation
                {
                    Id = Guid.NewGuid(),
                    EventId = targetEvent.Id,
                    UserId = currentUserId.Value,
                    JoinedAt = DateTime.UtcNow
                };

                _context.EventParticipations.Add(newParticipation);
                targetEvent.Participations.Add(newParticipation);

                if (targetEvent.Participations.Count >= targetEvent.RequiredVolunteers)
                {
                    targetEvent.Status = EventStatus.QuotaFull;
                }

                actionType = "joined";
                message = "Tebrikler! Etkinliğe başarıyla katıldınız. Kampüs doğası size minnettar.";
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            var currentCount = targetEvent.Participations.Count;
            var isQuotaFull = currentCount >= targetEvent.RequiredVolunteers;

            _logger.LogInformation("Kullanıcı {UserId} etkinlik {EventId} için {Action} işlemi gerçekleştirdi. Yeni Sayı: {Count}/{Req}",
                currentUserId.Value, targetEvent.Id, actionType, currentCount, targetEvent.RequiredVolunteers);

            return Ok(new
            {
                success = true,
                action = actionType,
                message,
                currentParticipantsCount = currentCount,
                requiredVolunteers = targetEvent.RequiredVolunteers,
                isQuotaFull,
                status = targetEvent.Status,
                statusText = targetEvent.Status switch
                {
                    EventStatus.Open => "Gönüllü Aranıyor",
                    EventStatus.QuotaFull => "Kontenjan Doldu",
                    _ => targetEvent.Status.ToString()
                }
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "ToggleJoin sırasında hata oluştu. EventId: {EventId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = "İşlem sırasında beklenmeyen bir hata oluştu." });
        }
        finally
        {
            _concurrencyLock.Release();
        }
    }

    /// <summary>
    /// TEMİZLİK KANITI YÜKLEME VE İLANI TAMAMLAMA (POST /api/events/{id}/complete)
    /// İlanın kurucusu veya katılımcı gönüllüler temizlenmiş alanın (After) fotoğrafını yükler.
    /// Etkinlik Completed durumuna geçer; tüm katılımcılar +30 bonus puan, yükleyen +10 puan kazanır.
    /// </summary>
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    [Route("api/events/{id:guid}/complete")]
    public async Task<IActionResult> CompleteEvent(
        Guid id,
        [FromForm] IFormFile afterPhoto,
        [FromForm] string? completionNote)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return Unauthorized(new { success = false, message = "Bu işlem için oturum açmanız gerekmektedir." });
        }

        var targetEvent = await _context.CleanupEvents
            .Include(e => e.Creator)
            .Include(e => e.Participations)
                .ThenInclude(p => p.User)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (targetEvent == null)
        {
            return NotFound(new { success = false, message = "Tamamlanmak istenen etkinlik bulunamadı." });
        }

        // 1. Yetki Kontrolü: İlanın kurucusu VEYA katılan gönüllülerinden biri olmalı (ya da Koordinatör)
        bool isCreator = targetEvent.CreatorId == currentUserId.Value;
        bool isParticipant = targetEvent.Participations.Any(p => p.UserId == currentUserId.Value);
        bool isCoordinatorOrAdmin = User.IsInRole("Coordinator") || User.IsInRole("Admin");

        if (!isCreator && !isParticipant && !isCoordinatorOrAdmin)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                success = false,
                message = "Temizlik sonrası fotoğrafını yalnızca bu ilanın kurucusu veya etkinliğe katılmış olan gönüllüler ekleyebilir."
            });
        }

        // 2. Dosya Doğrulaması
        if (afterPhoto == null || afterPhoto.Length == 0)
        {
            return BadRequest(new { success = false, message = "Lütfen temizlik sonrasını gösteren fotoğrafı yükleyiniz." });
        }

        // 3. Güvenli Dosya Yükleme Servisi (Magic Bytes, Uzantı ve 5MB Kontrolü)
        string afterImagePath;
        try
        {
            afterImagePath = await _fileStorageService.SaveSpotImageAsync(afterPhoto);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Sonrası fotoğrafı yüklenirken hata oluştu.");
            return BadRequest(new { success = false, message = ex.Message });
        }

        // 4. Veritabanı Güncellemesi
        await _concurrencyLock.WaitAsync();
        try
        {
            // Varsa eski sonrası fotoğrafını temizle
            if (!string.IsNullOrWhiteSpace(targetEvent.AfterImageUrl) && targetEvent.AfterImageUrl != afterImagePath)
            {
                await _fileStorageService.DeleteImageAsync(targetEvent.AfterImageUrl);
            }

            targetEvent.Status = EventStatus.Completed;
            targetEvent.AfterImageUrl = afterImagePath;
            targetEvent.CompletedAt = DateTime.UtcNow;
            targetEvent.CompletedByUserId = currentUserId.Value;
            if (!string.IsNullOrWhiteSpace(completionNote))
            {
                targetEvent.CompletionNote = completionNote.Trim();
            }
            else if (string.IsNullOrWhiteSpace(targetEvent.CompletionNote))
            {
                targetEvent.CompletionNote = "Temizlik başarıyla tamamlandı, alan tertemiz hale getirildi.";
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Etkinlik tamamlandı ve kanıt yüklendi: {EventId} (Yükleyen: {UserId})", id, currentUserId.Value);

            var responseDto = new EventCardDto
            {
                Id = targetEvent.Id,
                Title = targetEvent.Title,
                Description = targetEvent.Description,
                LocationName = targetEvent.LocationName,
                Latitude = targetEvent.Latitude,
                Longitude = targetEvent.Longitude,
                ImageUrl = targetEvent.ImageUrl,
                AfterImageUrl = targetEvent.AfterImageUrl,
                ScheduledAt = targetEvent.ScheduledAt,
                CompletedAt = targetEvent.CompletedAt,
                CompletedByUserId = targetEvent.CompletedByUserId,
                CompletionNote = targetEvent.CompletionNote,
                Status = targetEvent.Status,
                RequiredVolunteers = targetEvent.RequiredVolunteers,
                CurrentParticipantsCount = targetEvent.Participations.Count,
                IsQuotaFull = true,
                HasCurrentUserJoined = targetEvent.Participations.Any(p => p.UserId == currentUserId.Value),
                IsCreator = isCreator,
                CanCurrentUserComplete = false,
                CreatorName = targetEvent.Creator.FullName,
                CreatedAt = targetEvent.CreatedAt,
                ParticipantsList = targetEvent.Participations
                    .OrderBy(p => p.JoinedAt)
                    .Select(p => new ParticipantSummaryDto
                    {
                        UserId = p.UserId,
                        FullName = p.User.FullName,
                        Initials = GetInitials(p.User.FullName),
                        JoinedAt = p.JoinedAt
                    }).ToList()
            };

            return Ok(new
            {
                success = true,
                message = "Tebrikler! Temizlik kanıtlandı, tüm katılımcılar +30 Bonus Puan kazandı!",
                data = responseDto
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Etkinlik tamamlanırken hata oluştu: {EventId}", id);
            await _fileStorageService.DeleteImageAsync(afterImagePath);
            return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = "Kayıt sırasında bir sunucu hatası oluştu." });
        }
        finally
        {
            _concurrencyLock.Release();
        }
    }

    /// <summary>
    /// Güvenli İlan Silme Endpoint'i (DELETE /api/events/{id})
    /// IDOR Güvenlik Kontrolü: Yalnızca ilanın sahibi (CreatorId) veya Sistem Koordinatörü/Admini silebilir.
    /// 1. Katılım kayıtları (EventParticipations) temizlenir.
    /// 2. Yüklenen kirlilik fotoğrafı wwwroot/uploads/spots/ altından fiziksel olarak silinir.
    /// 3. İlan veritabanından kalıcı olarak kaldırılır.
    /// </summary>
    [HttpDelete]
    [Authorize]
    [ValidateAntiForgeryToken]
    [Route("api/events/{id:guid}")]
    public async Task<IActionResult> DeleteEvent(Guid id)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return Unauthorized(new { success = false, message = "Bu işlem için oturum açmanız gerekmektedir." });
        }

        // 1. İlanı ve katılımcı kayıtlarını getir
        var targetEvent = await _context.CleanupEvents
            .Include(e => e.Participations)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (targetEvent == null)
        {
            return NotFound(new { success = false, message = "Silinmek istenen kirlilik ilanı bulunamadı." });
        }

        // 2. Güvenlik Kontrolü (IDOR Önleme):
        bool isCoordinatorOrAdmin = User.IsInRole("Coordinator") || User.IsInRole("Admin");
        if (targetEvent.CreatorId != currentUserId.Value && !isCoordinatorOrAdmin)
        {
            _logger.LogWarning("IDOR İhlali Teşebbüsü Engellendi! Kullanıcı: {UserId}, İlan: {EventId}, Sahip: {CreatorId}",
                currentUserId.Value, targetEvent.Id, targetEvent.CreatorId);

            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                success = false,
                message = "Bu ilanı silme yetkiniz bulunmamaktadır. Yalnızca kendi açtığınız ilanları silebilirsiniz."
            });
        }

        var imageToDelete = targetEvent.ImageUrl;
        var afterImageToDelete = targetEvent.AfterImageUrl;

        // 3. Veritabanı ve Fiziksel Temizlik
        await _concurrencyLock.WaitAsync();
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            if (targetEvent.Participations != null && targetEvent.Participations.Count > 0)
            {
                _context.EventParticipations.RemoveRange(targetEvent.Participations);
            }

            _context.CleanupEvents.Remove(targetEvent);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Kirlilik ilanı ve ilişkili kayıtlar veritabanından silindi: {EventId} (Başlık: {Title})", id, targetEvent.Title);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "İlan silinirken veritabanı hatası oluştu: {EventId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = "İlan silinirken bir veritabanı hatası oluştu."
            });
        }
        finally
        {
            _concurrencyLock.Release();
        }

        // 4. IFileStorageService üzerinden fiziksel fotoğrafları sil
        try
        {
            if (!string.IsNullOrWhiteSpace(imageToDelete))
            {
                await _fileStorageService.DeleteImageAsync(imageToDelete);
            }
            if (!string.IsNullOrWhiteSpace(afterImageToDelete))
            {
                await _fileStorageService.DeleteImageAsync(afterImageToDelete);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "İlan silindi ancak fotoğraf dosyaları temizlenirken bir sorun oluştu: {Image}", imageToDelete);
        }

        return Ok(new
        {
            success = true,
            message = "Kirlilik ilanı, katılım kayıtları ve yüklenen fotoğraflar başarıyla silindi."
        });
    }

    private Guid? GetCurrentUserId()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(idClaim, out var userId))
        {
            return userId;
        }
        return null;
    }

    private static string GetInitials(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return "??";

        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
            return parts[0][0].ToString().ToUpper();

        return $"{char.ToUpper(parts[0][0])}.{char.ToUpper(parts[^1][0])}.";
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View("~/Views/Shared/Error.cshtml", new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
