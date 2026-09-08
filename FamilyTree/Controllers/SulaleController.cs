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
    private const int BulkAssignPageSize = 20;

    private readonly ApplicationDbContext _context;
    private readonly IPersonService _personService;
    private readonly IAuditLogService _auditLogService;

    public SulaleController(ApplicationDbContext context, IPersonService personService, IAuditLogService auditLogService)
    {
        _context = context;
        _personService = personService;
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
    public async Task<IActionResult> BulkAssign(string? q, int page = 1)
    {
        var vm = new BulkSulaleViewModel
        {
            Query = q,
            Page = Math.Max(1, page),
            PageSize = BulkAssignPageSize,
            Sulaleler = await GetSulaleListAsync(),
        };

        if (!string.IsNullOrWhiteSpace(q))
        {
            var result = await _personService.SearchAsync(q, vm.Page, BulkAssignPageSize);
            vm.Searched = true;
            vm.Persons = result.Persons;
            vm.Page = result.Page;
            vm.TotalCount = result.TotalCount;
        }

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> BulkAssign(int sulaleId, int[]? personIds, string? q, int page = 1)
    {
        var ids = (personIds ?? Array.Empty<int>()).Distinct().ToList();

        if (ids.Count == 0)
        {
            TempData["ErrorMessage"] = "Aktarılacak kişi seçilmedi.";
            return RedirectToAction(nameof(BulkAssign), new { q, page });
        }

        var sulale = await _context.Sulaleler.FirstOrDefaultAsync(s => s.Id == sulaleId);
        if (sulale == null)
        {
            TempData["ErrorMessage"] = "Seçilen sülale bulunamadı.";
            return RedirectToAction(nameof(BulkAssign), new { q, page });
        }

        var validPersonIds = await _context.Persons
            .Where(p => ids.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync();

        var alreadyLinked = await _context.PersonSulaleler
            .Where(ps => ps.SulaleId == sulaleId && validPersonIds.Contains(ps.PersonId))
            .Select(ps => ps.PersonId)
            .ToListAsync();

        var toAdd = validPersonIds.Except(alreadyLinked).ToList();

        foreach (var personId in toAdd)
        {
            _context.PersonSulaleler.Add(new PersonSulale
            {
                PersonId = personId,
                SulaleId = sulaleId,
                CreatedAt = DateTime.UtcNow,
            });
        }

        if (toAdd.Count > 0)
        {
            await _context.SaveChangesAsync();
            await _auditLogService.LogAsync($"{toAdd.Count} kişi toplu olarak '{sulale.Ad}' sülalesine eklendi", "Sulale", sulaleId);
            TempData["SuccessMessage"] = $"{toAdd.Count} kişi '{sulale.Ad}' sülalesine eklendi." +
                (alreadyLinked.Count > 0 ? $" {alreadyLinked.Count} kişi zaten bu sülalede olduğu için atlandı." : string.Empty);
        }
        else
        {
            TempData["ErrorMessage"] = "Seçilen kişilerin tamamı zaten bu sülalede.";
        }

        return RedirectToAction(nameof(BulkAssign), new { q, page });
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

    private async Task<List<SulaleListItemViewModel>> GetSulaleListAsync()
    {
        return await _context.Sulaleler
            .AsNoTracking()
            .OrderBy(s => s.Ad)
            .Select(s => new SulaleListItemViewModel { Id = s.Id, Ad = s.Ad })
            .ToListAsync();
    }
}
