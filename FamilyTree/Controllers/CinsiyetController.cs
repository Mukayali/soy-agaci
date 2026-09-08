using FamilyTree.Models;
using FamilyTree.Services;
using FamilyTree.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FamilyTree.Controllers;

[Authorize(Roles = "Admin")]
public class CinsiyetController : Controller
{
    private const int MaxFileSizeBytes = 5 * 1024 * 1024;
    private const int MissingPageSize = 100;

    private static readonly string[] AllowedExtensions = { ".xlsx", ".csv" };

    private readonly IGenderReferenceService _service;
    private readonly IAuditLogService _auditLogService;

    public CinsiyetController(IGenderReferenceService service, IAuditLogService auditLogService)
    {
        _service = service;
        _auditLogService = auditLogService;
    }

    public async Task<IActionResult> Index(bool eksik = false, int page = 1)
    {
        var vm = new CinsiyetYonetimViewModel
        {
            ReferansSayisi = await _service.GetReferenceCountAsync(),
            ReferansOrnek = await _service.GetReferenceSampleAsync(),
            EksikleriGoster = eksik,
            SayfaNo = Math.Max(1, page),
            SayfaBoyutu = MissingPageSize,
        };

        if (eksik)
        {
            var (persons, total) = await _service.GetMissingGenderAsync(vm.SayfaNo, MissingPageSize);
            vm.EksikToplam = total;
            vm.EksikKisiler = persons.Select(p => new EksikCinsiyetKisiViewModel
            {
                Id = p.Id,
                Ad = p.Ad,
                Soyad = p.Soyad,
                DogumYili = p.DogumTarihi?.Year,
                OlumYili = p.OlumTarihi?.Year,
                AnneAdSoyad = p.Anne == null ? null : $"{p.Anne.Ad} {p.Anne.Soyad}",
                BabaAdSoyad = p.Baba == null ? null : $"{p.Baba.Ad} {p.Baba.Soyad}",
            }).ToList();
        }

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            TempData["ErrorMessage"] = "Lütfen bir dosya seçin.";
            return RedirectToAction(nameof(Index));
        }

        if (file.Length > MaxFileSizeBytes)
        {
            TempData["ErrorMessage"] = "Dosya boyutu 5 MB'ı aşamaz.";
            return RedirectToAction(nameof(Index));
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            TempData["ErrorMessage"] = "Yalnızca .xlsx veya .csv dosyaları kabul edilir.";
            return RedirectToAction(nameof(Index));
        }

        await using var stream = file.OpenReadStream();
        var result = await _service.ImportReferenceAsync(stream, extension);

        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage ?? "Dosya içe aktarılamadı.";
            return RedirectToAction(nameof(Index));
        }

        await _auditLogService.LogAsync(
            $"Cinsiyet referans listesi yüklendi ({result.Added} eklendi, {result.Updated} güncellendi, {result.Skipped} atlandı)",
            "AdCinsiyet");

        TempData["SuccessMessage"] =
            $"{result.Added} yeni ad eklendi, {result.Updated} kayıt güncellendi." +
            (result.Skipped > 0 ? $" {result.Skipped} satır cinsiyeti okunamadığı için atlandı." : string.Empty);

        if (result.Warnings.Count > 0)
        {
            TempData["ImportWarnings"] = string.Join("\n", result.Warnings.Take(30));
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Clear()
    {
        var removed = await _service.ClearReferenceAsync();
        await _auditLogService.LogAsync($"Cinsiyet referans listesi temizlendi ({removed} kayıt)", "AdCinsiyet");
        TempData["SuccessMessage"] = $"Referans listesi temizlendi ({removed} kayıt silindi).";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Match(bool overwriteExisting = false)
    {
        if (await _service.GetReferenceCountAsync() == 0)
        {
            TempData["ErrorMessage"] = "Önce bir referans listesi (Excel/CSV) yükleyin.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _service.MatchByNamesAsync(overwriteExisting);

        await _auditLogService.LogAsync(
            $"Adlara göre cinsiyet eşlemesi çalıştırıldı ({result.Updated} güncellendi, overwrite={overwriteExisting})",
            "Person");

        TempData["SuccessMessage"] =
            $"{result.Updated} kişinin cinsiyeti güncellendi. " +
            $"{result.AlreadyCorrect} kişi zaten doğruydu, {result.NoMatch} kişinin adı referans listesinde bulunamadı.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkUpdateMissing(Dictionary<int, string>? genders, int page = 1)
    {
        var assignments = new Dictionary<int, Gender>();
        if (genders != null)
        {
            foreach (var (id, value) in genders)
            {
                if (Enum.TryParse<Gender>(value, ignoreCase: true, out var gender))
                {
                    assignments[id] = gender;
                }
            }
        }

        if (assignments.Count == 0)
        {
            TempData["ErrorMessage"] = "Hiçbir kişi için cinsiyet seçilmedi.";
            return RedirectToAction(nameof(Index), new { eksik = true, page });
        }

        var count = await _service.BulkSetGendersAsync(assignments);
        await _auditLogService.LogAsync($"{count} kişinin cinsiyeti toplu olarak güncellendi", "Person");
        TempData["SuccessMessage"] = $"{count} kişinin cinsiyeti güncellendi.";

        return RedirectToAction(nameof(Index), new { eksik = true, page });
    }
}
