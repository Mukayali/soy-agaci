using FamilyTree.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FamilyTree.Controllers;

public class GedcomController : Controller
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    private readonly IGedcomService _gedcomService;
    private readonly IAuditLogService _auditLogService;

    public GedcomController(IGedcomService gedcomService, IAuditLogService auditLogService)
    {
        _gedcomService = gedcomService;
        _auditLogService = auditLogService;
    }

    public IActionResult Index()
    {
        return View();
    }

    public async Task<IActionResult> Export()
    {
        var bytes = await _gedcomService.ExportAsync();
        await _auditLogService.LogAsync("Soy ağacı GEDCOM olarak dışa aktarıldı", "Person");

        return File(bytes, "text/vnd.familysearch.gedcom", $"soy-agaci-{DateTime.UtcNow:yyyyMMdd-HHmmss}.ged");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> Import(IFormFile? gedcomFile)
    {
        if (gedcomFile == null || gedcomFile.Length == 0)
        {
            TempData["ErrorMessage"] = "Lütfen bir GEDCOM (.ged) dosyası seçin.";
            return RedirectToAction(nameof(Index));
        }

        if (gedcomFile.Length > MaxFileSizeBytes)
        {
            TempData["ErrorMessage"] = "Dosya boyutu 10 MB'ı aşamaz.";
            return RedirectToAction(nameof(Index));
        }

        var extension = Path.GetExtension(gedcomFile.FileName).ToLowerInvariant();
        if (extension != ".ged")
        {
            TempData["ErrorMessage"] = "Yalnızca .ged uzantılı dosyalar kabul edilir.";
            return RedirectToAction(nameof(Index));
        }

        using var memoryStream = new MemoryStream();
        await gedcomFile.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        if (!await LooksLikeGedcomAsync(memoryStream))
        {
            TempData["ErrorMessage"] = "Dosya geçerli bir GEDCOM dosyasına benzemiyor (ilk satır '0 HEAD' ile başlamalı).";
            return RedirectToAction(nameof(Index));
        }

        memoryStream.Position = 0;
        var result = await _gedcomService.ImportAsync(memoryStream);

        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage ?? "İçe aktarma başarısız oldu.";
            return RedirectToAction(nameof(Index));
        }

        await _auditLogService.LogAsync(
            $"GEDCOM içe aktarıldı ({result.PersonsCreated} kişi, {result.SpouseRelationshipsCreated} eş ilişkisi)",
            "Person");

        TempData["SuccessMessage"] =
            $"{result.PersonsCreated} kişi ve {result.SpouseRelationshipsCreated} eş ilişkisi içe aktarıldı " +
            $"({result.FamiliesProcessed} aile kaydı işlendi).";

        if (result.Warnings.Count > 0)
        {
            TempData["ImportWarnings"] = string.Join("\n", result.Warnings.Take(50));
        }

        return RedirectToAction(nameof(Index));
    }

    private static async Task<bool> LooksLikeGedcomAsync(Stream stream)
    {
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);

        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            return trimmed.StartsWith("0 HEAD", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
