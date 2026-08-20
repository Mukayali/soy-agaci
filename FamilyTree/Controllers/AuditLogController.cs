using FamilyTree.Data;
using FamilyTree.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FamilyTree.Controllers;

[Authorize(Roles = "Admin")]
public class AuditLogController : Controller
{
    private const int PageSize = 50;

    private readonly ApplicationDbContext _context;

    public AuditLogController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(int page = 1)
    {
        page = Math.Max(1, page);

        var query = _context.AuditLogs.AsNoTracking().OrderByDescending(a => a.Tarih);

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(a => new AuditLogItemViewModel
            {
                Id = a.Id,
                KullaniciAdi = a.KullaniciAdi,
                Islem = a.Islem,
                Entity = a.Entity,
                EntityId = a.EntityId,
                Ip = a.Ip,
                Tarih = a.Tarih,
            })
            .ToListAsync();

        return View(new AuditLogListViewModel
        {
            Items = items,
            Page = page,
            PageSize = PageSize,
            TotalCount = totalCount,
        });
    }
}
