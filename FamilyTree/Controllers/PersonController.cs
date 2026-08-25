using System.Text;
using FamilyTree.Data;
using FamilyTree.Models;
using FamilyTree.Services;
using FamilyTree.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FamilyTree.Controllers;

public class PersonController : Controller
{
    private const long MaxCsvFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    private readonly IPersonService _personService;
    private readonly IPhotoService _photoService;
    private readonly IAuditLogService _auditLogService;
    private readonly ICsvImportService _csvImportService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PersonController> _logger;

    public PersonController(
        IPersonService personService,
        IPhotoService photoService,
        IAuditLogService auditLogService,
        ICsvImportService csvImportService,
        ApplicationDbContext context,
        ILogger<PersonController> logger)
    {
        _personService = personService;
        _photoService = photoService;
        _auditLogService = auditLogService;
        _csvImportService = csvImportService;
        _context = context;
        _logger = logger;
    }

    public async Task<IActionResult> Index(string? q, int page = 1, int? sulaleId = null)
    {
        var result = await _personService.SearchAsync(q, page, sulaleId: sulaleId);
        return View(result);
    }

    public async Task<IActionResult> Details(int id)
    {
        var vm = await _personService.GetDetailsAsync(id);
        if (vm == null)
        {
            return NotFound();
        }

        return View(vm);
    }

    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> Create()
    {
        await PopulateSulalelerAsync();
        return View(new PersonCreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> Create(PersonCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateSulalelerAsync();
            return View(model);
        }

        var (success, id, errorMessage) = await _personService.CreateAsync(model);

        if (!success)
        {
            ModelState.AddModelError(string.Empty, errorMessage ?? "Kişi kaydedilemedi.");
            await PopulateSulalelerAsync();
            return View(model);
        }

        await _auditLogService.LogAsync("Kişi oluşturuldu", "Person", id);
        TempData["SuccessMessage"] = "Kişi başarıyla eklendi.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> Edit(int id)
    {
        var vm = await _personService.GetForEditAsync(id);
        if (vm == null)
        {
            return NotFound();
        }

        await PopulateSulalelerAsync();
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> Edit(int id, PersonEditViewModel model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            await PopulateSulalelerAsync();
            return View(model);
        }

        var (success, errorMessage) = await _personService.UpdateAsync(model);

        if (!success)
        {
            ModelState.AddModelError(string.Empty, errorMessage ?? "Kişi güncellenemedi.");
            await PopulateSulalelerAsync();
            return View(model);
        }

        await _auditLogService.LogAsync("Kişi güncellendi", "Person", id);
        TempData["SuccessMessage"] = "Kişi başarıyla güncellendi.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var (success, errorMessage, childCount, spouseCount) = await _personService.GetDeleteInfoAsync(id);
        if (!success)
        {
            return NotFound();
        }

        var detail = await _personService.GetDetailsAsync(id);
        if (detail == null)
        {
            return NotFound();
        }

        ViewBag.ChildCount = childCount;
        ViewBag.SpouseCount = spouseCount;

        return View(detail);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        await _personService.DeleteAsync(id);
        await _auditLogService.LogAsync("Kişi silindi", "Person", id);
        TempData["SuccessMessage"] = "Kişi silindi.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Deleted()
    {
        var persons = await _personService.GetDeletedAsync();
        return View(persons);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Restore(int id)
    {
        var (success, errorMessage) = await _personService.RestoreAsync(id);

        if (!success)
        {
            TempData["ErrorMessage"] = errorMessage ?? "Kişi geri getirilemedi.";
        }
        else
        {
            await _auditLogService.LogAsync("Kişi geri getirildi", "Person", id);
            TempData["SuccessMessage"] = "Kişi başarıyla geri getirildi.";
        }

        return RedirectToAction(nameof(Deleted));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> DeletePhoto(int photoId, int personId)
    {
        await _photoService.DeletePhotoAsync(photoId);
        await _auditLogService.LogAsync("Fotoğraf silindi", "Person", personId);
        return RedirectToAction(nameof(Edit), new { id = personId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> SetPrimaryPhoto(int photoId, int personId)
    {
        await _photoService.SetPrimaryAsync(personId, photoId);
        return RedirectToAction(nameof(Edit), new { id = personId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> AddSpouse(int personId, int spouseId, DateTime? marriageDate)
    {
        if (personId == spouseId)
        {
            TempData["ErrorMessage"] = "Bir kişi kendisiyle evli olamaz.";
            return RedirectToAction(nameof(Details), new { id = personId });
        }

        var exists = await _context.SpouseRelationships.AnyAsync(sr =>
            (sr.Person1Id == personId && sr.Person2Id == spouseId) ||
            (sr.Person1Id == spouseId && sr.Person2Id == personId));

        if (!exists)
        {
            var bothExist = await _context.Persons.CountAsync(p => p.Id == personId || p.Id == spouseId) == 2;
            if (bothExist)
            {
                _context.SpouseRelationships.Add(new SpouseRelationship
                {
                    Person1Id = personId,
                    Person2Id = spouseId,
                    MarriageDate = marriageDate,
                });
                await _context.SaveChangesAsync();
                await _auditLogService.LogAsync("Eş ilişkisi eklendi", "Person", personId);
            }
        }

        return RedirectToAction(nameof(Details), new { id = personId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> RemoveSpouse(int spouseRelationshipId, int personId)
    {
        var relationship = await _context.SpouseRelationships.FindAsync(spouseRelationshipId);
        if (relationship != null)
        {
            _context.SpouseRelationships.Remove(relationship);
            await _context.SaveChangesAsync();
            await _auditLogService.LogAsync("Eş ilişkisi kaldırıldı", "Person", personId);
        }

        return RedirectToAction(nameof(Details), new { id = personId });
    }

    public async Task<IActionResult> ExportCsv(string? q)
    {
        const int maxRows = 5000;

        var result = await _personService.SearchAsync(q, page: 1, pageSize: maxRows);

        var sb = new StringBuilder();
        sb.AppendLine("Ad Soyad,Dogum Tarihi,Olum Tarihi,Anne,Baba");

        foreach (var p in result.Persons)
        {
            sb.AppendLine(string.Join(",",
                CsvField(p.AdSoyad),
                CsvField(p.DogumTarihi?.ToString("yyyy-MM-dd")),
                CsvField(p.OlumTarihi?.ToString("yyyy-MM-dd")),
                CsvField(p.AnneAdSoyad),
                CsvField(p.BabaAdSoyad)));
        }

        await _auditLogService.LogAsync("Kişi listesi CSV olarak dışa aktarıldı", "Person", null);

        var utf8Bom = Encoding.UTF8.GetPreamble();
        var content = Encoding.UTF8.GetBytes(sb.ToString());
        var bytes = new byte[utf8Bom.Length + content.Length];
        utf8Bom.CopyTo(bytes, 0);
        content.CopyTo(bytes, utf8Bom.Length);

        return File(bytes, "text/csv", $"kisiler-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> ImportCsv(IFormFile? csvFile)
    {
        if (csvFile == null || csvFile.Length == 0)
        {
            TempData["ErrorMessage"] = "Lütfen bir CSV dosyası seçin.";
            return RedirectToAction(nameof(Index));
        }

        if (csvFile.Length > MaxCsvFileSizeBytes)
        {
            TempData["ErrorMessage"] = "Dosya boyutu 5 MB'ı aşamaz.";
            return RedirectToAction(nameof(Index));
        }

        var extension = Path.GetExtension(csvFile.FileName).ToLowerInvariant();
        if (extension != ".csv")
        {
            TempData["ErrorMessage"] = "Yalnızca .csv uzantılı dosyalar kabul edilir.";
            return RedirectToAction(nameof(Index));
        }

        await using var stream = csvFile.OpenReadStream();
        var result = await _csvImportService.ImportAsync(stream);

        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage ?? "İçe aktarma başarısız oldu.";
            return RedirectToAction(nameof(Index));
        }

        await _auditLogService.LogAsync(
            $"CSV içe aktarıldı ({result.PersonsCreated} kişi eklendi, {result.PersonsUpdated} kişi güncellendi, {result.RelationshipsLinked} anne/baba ilişkisi)",
            "Person");

        TempData["SuccessMessage"] =
            $"{result.PersonsCreated} kişi eklendi, {result.PersonsUpdated} kişi güncellendi, {result.RelationshipsLinked} anne/baba ilişkisi kuruldu.";

        if (result.Warnings.Count > 0)
        {
            TempData["ImportWarnings"] = string.Join("\n", result.Warnings.Take(50));
        }

        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin,Editor")]
    public IActionResult CsvImportTemplate()
    {
        var sb = new StringBuilder();
        sb.AppendLine("TCKimlikNo,Ad,Soyad,Cinsiyet,DogumTarihi,OlumTarihi,AnneTC,BabaTC,DogumYeri,SulaleId");
        sb.AppendLine("11111111110,Hasan,Demir,Erkek,1940-01-15,2015-06-20,,,Erzurum,");
        sb.AppendLine("22222222220,Fatma,Demir,Kadin,1943-03-10,,,,Erzurum,");
        sb.AppendLine("33333333330,Mehmet,Demir,Erkek,1965-07-01,,22222222220,11111111110,Erzurum,");
        sb.AppendLine("44444444440,Ayşe,Kaya,Kadin,1968-09-05,,,,Ankara,");
        sb.AppendLine("55555555550,Ali,Demir,Erkek,1990-05-05,,44444444440,33333333330,İstanbul,");

        var utf8Bom = Encoding.UTF8.GetPreamble();
        var content = Encoding.UTF8.GetBytes(sb.ToString());
        var bytes = new byte[utf8Bom.Length + content.Length];
        utf8Bom.CopyTo(bytes, 0);
        content.CopyTo(bytes, utf8Bom.Length);

        return File(bytes, "text/csv", "kisiler-sablon.csv");
    }

    private async Task PopulateSulalelerAsync()
    {
        ViewBag.Sulaleler = await _context.Sulaleler
            .AsNoTracking()
            .OrderBy(s => s.Ad)
            .Select(s => new SulaleListItemViewModel { Id = s.Id, Ad = s.Ad })
            .ToListAsync();
    }

    private static string CsvField(string? value)
    {
        value ??= string.Empty;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }
}
