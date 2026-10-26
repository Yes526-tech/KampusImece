using KampusImece.Data;
using KampusImece.Models.Entities;
using KampusImece.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KampusImece.Controllers;

public class LeaderboardController : Controller
{
    private readonly AppDbContext _context;
    private readonly ILogger<LeaderboardController> _logger;

    public LeaderboardController(AppDbContext context, ILogger<LeaderboardController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Liderlik Tablosu Sayfa Görünümü (GET /Leaderboard)
    /// </summary>
    [HttpGet]
    [Route("Leaderboard")]
    [Route("Leaderboard/Index")]
    public async Task<IActionResult> Index()
    {
        var leaderboard = await GetLeaderboardDataAsync();
        return View(leaderboard);
    }

    /// <summary>
    /// Liderlik Tablosu JSON API Endpoint'i (GET /api/leaderboard)
    /// </summary>
    [HttpGet]
    [Route("api/leaderboard")]
    public async Task<IActionResult> GetLeaderboardApi()
    {
        var leaderboard = await GetLeaderboardDataAsync();
        return Ok(leaderboard);
    }

    private async Task<List<LeaderboardEntryDto>> GetLeaderboardDataAsync()
    {
        // 1. Veritabanındaki tüm öğrencilerin oluşturduğu ilan, katılım ve tamamlanmış temizlik verilerini çek
        var rawData = await _context.Users
            .Select(u => new
            {
                u.Id,
                u.FullName,
                u.StudentNumber,
                u.ReliabilityScore,
                CreatedEventsCount = _context.CleanupEvents.Count(e => e.CreatorId == u.Id),
                JoinedEventsCount = _context.EventParticipations.Count(p => p.UserId == u.Id),
                // Kullanıcının kurucusu olduğu VEYA katıldığı ve başarıyla kanıtlanan tamamlanmış etkinlik sayısı (+30 Bonus Puan)
                CompletedEventsCount = _context.CleanupEvents.Count(e => 
                    e.Status == EventStatus.Completed && 
                    (e.CreatorId == u.Id || e.Participations.Any(p => p.UserId == u.Id))),
                // Kullanıcının bizzat kanıt fotoğrafı yüklediği temizlik sayısı (+10 Ekstra Puan)
                ProofUploadedCount = _context.CleanupEvents.Count(e => e.CompletedByUserId == u.Id)
            })
            .ToListAsync();

        // 2. Dinamik Puanlama Formülü:
        // - Açtığı her kirlilik ilanı = +25 Puan
        // - Katıldığı her temizlik etkinliği = +15 Puan
        // - Tamamlanmış (kanıtlanmış) her temizlik = +30 Bonus Puan
        // - Kanıt fotoğrafını bizzat yükleyen kişiye = +10 Ekstra Puan
        var rankedUsers = rawData
            .Select(u => new
            {
                u.Id,
                u.FullName,
                u.StudentNumber,
                u.ReliabilityScore,
                u.CreatedEventsCount,
                u.JoinedEventsCount,
                u.CompletedEventsCount,
                u.ProofUploadedCount,
                TotalScore = (u.CreatedEventsCount * 25) + 
                             (u.JoinedEventsCount * 15) + 
                             (u.CompletedEventsCount * 30) + 
                             (u.ProofUploadedCount * 10)
            })
            .OrderByDescending(u => u.TotalScore)
            .ThenByDescending(u => u.CompletedEventsCount)
            .ThenByDescending(u => u.JoinedEventsCount)
            .ThenByDescending(u => u.CreatedEventsCount)
            .ThenBy(u => u.FullName)
            .Take(20)
            .ToList();

        // 3. Sıralama ve DTO üretimi
        var result = rankedUsers.Select((u, index) => new LeaderboardEntryDto
        {
            Rank = index + 1,
            UserId = u.Id,
            FullName = u.FullName,
            StudentNumber = LeaderboardEntryDto.MaskStudentNumber(u.StudentNumber),
            CreatedEventsCount = u.CreatedEventsCount,
            JoinedEventsCount = u.JoinedEventsCount,
            CompletedEventsCount = u.CompletedEventsCount,
            ProofUploadedCount = u.ProofUploadedCount,
            TotalScore = u.TotalScore,
            ReliabilityScore = u.ReliabilityScore,
            BadgeTitle = LeaderboardEntryDto.DetermineBadge(u.TotalScore),
            Initials = GetInitials(u.FullName)
        }).ToList();

        return result;
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
}
