using FamilyTree.Data;
using FamilyTree.Models;
using FamilyTree.Services;
using FamilyTree.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FamilyTree.Controllers;

public class SulaleController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditLogService _auditLogService;

    public SulaleController(ApplicationDbContext context, IAuditLogService auditLogService)
    {
        _context = context;
        _auditLogService = auditLogService;
    }

    public async Task<IActionResult> Index()
    {
        var sulaleler = await _context.Sulaleler
            .AsNoTracking()
            .OrderBy(s => s.Ad)
            .Select(s => new SulaleListItemViewModel
            {
                Id = s.Id,
                Ad = s.Ad,
                Aciklama = s.Aciklama,
                UyeSayisi = s.PersonSulaleler.Count,
            })
            .ToListAsync();

        return View(sulaleler);
    }

    [Authorize(Roles = "Admin,Editor")]
    public IActionResult Create()
    {
        return View(new SulaleCreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> Create(SulaleCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var exists = await _context.Sulaleler.AnyAsync(s => s.Ad == model.Ad.Trim());
        if (exists)
        {
            ModelState.AddModelError(nameof(model.Ad), "Bu isimde bir sülale zaten kayıtlı.");
            return View(model);
        }

        var sulale = new Sulale
        {
            Ad = model.Ad.Trim(),
            Aciklama = model.Aciklama,
            CreatedAt = DateTime.UtcNow,
        };

        _context.Sulaleler.Add(sulale);
        await _context.SaveChangesAsync();

        await _auditLogService.LogAsync("Sülale oluşturuldu", "Sulale", sulale.Id);
        TempData["SuccessMessage"] = "Sülale başarıyla eklendi.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> Edit(int id)
    {
        var sulale = await _context.Sulaleler.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        if (sulale == null)
        {
            return NotFound();
        }

        return View(new SulaleEditViewModel { Id = sulale.Id, Ad = sulale.Ad, Aciklama = sulale.Aciklama });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> Edit(int id, SulaleEditViewModel model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var sulale = await _context.Sulaleler.FirstOrDefaultAsync(s => s.Id == id);
        if (sulale == null)
        {
            return NotFound();
        }

        var exists = await _context.Sulaleler.AnyAsync(s => s.Ad == model.Ad.Trim() && s.Id != id);
        if (exists)
        {
            ModelState.AddModelError(nameof(model.Ad), "Bu isimde bir sülale zaten kayıtlı.");
            return View(model);
        }

        sulale.Ad = model.Ad.Trim();
        sulale.Aciklama = model.Aciklama;
        await _context.SaveChangesAsync();

        await _auditLogService.LogAsync("Sülale güncellendi", "Sulale", sulale.Id);
        TempData["SuccessMessage"] = "Sülale güncellendi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var sulale = await _context.Sulaleler.FirstOrDefaultAsync(s => s.Id == id);
        if (sulale != null)
        {
            _context.Sulaleler.Remove(sulale);
            await _context.SaveChangesAsync();
            await _auditLogService.LogAsync("Sülale silindi", "Sulale", id);
            TempData["SuccessMessage"] = "Sülale silindi. Üyelerin sülale etiketi kaldırıldı, kişi kayıtları silinmedi.";
        }

        return RedirectToAction(nameof(Index));
    }
}
