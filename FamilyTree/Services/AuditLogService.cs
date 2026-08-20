using FamilyTree.Data;
using FamilyTree.Models;
using Microsoft.AspNetCore.Http;

namespace FamilyTree.Services;

public class AuditLogService : IAuditLogService
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditLogService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogAsync(string islem, string entity, int? entityId = null)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        var log = new AuditLog
        {
            UserId = httpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            KullaniciAdi = httpContext?.User?.Identity?.Name,
            Islem = islem,
            Entity = entity,
            EntityId = entityId,
            Ip = httpContext?.Connection?.RemoteIpAddress?.ToString(),
            Tarih = DateTime.UtcNow,
        };

        _context.AuditLogs.Add(log);
        await _context.SaveChangesAsync();
    }
}
