using FamilyTree.Data;
using FamilyTree.Services;
using FamilyTree.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FamilyTree.Controllers;

public class PhotosController : Controller
{
    private static readonly int[] AllowedPageSizes = { 12, 24, 48, 96 };
    private const int DefaultPageSize = 24;

    private readonly ApplicationDbContext _context;
    private readonly IPhotoService _photoService;
    private readonly IAuditLogService _auditLogService;

    public PhotosController(ApplicationDbContext context, IPhotoService photoService, IAuditLogService auditLogService)
    {
        _context = context;
        _photoService = photoService;
        _auditLogService = auditLogService;
    }

    /// <summary>
    /// photoId verilirse (ör. Kişi Detay sayfasındaki "Düzenle" linkinden gelindiğinde), o
    /// fotoğrafı içeren sayfa otomatik hesaplanıp gösterilir; aksi halde normal sayfalama uygulanır.
    /// </summary>
    public async Task<IActionResult> Index(int page = 1, int pageSize = DefaultPageSize, int? photoId = null)
    {
        page = Math.Max(1, page);
        if (!AllowedPageSizes.Contains(pageSize))
        {
            pageSize = DefaultPageSize;
        }

        // IgnoreQueryFilters: Person üzerindeki "silinmemiş" global filtresi normalde Include
        // edilen gezinmelere de uygulanır. Bu, yumuşak silinmiş bir kişiye ait fotoğrafların
        // Person'ı sessizce null gelip hem grup hem "ilişkilendirilmemiş" listesinden düşmesine
        // (fotoğraf sayılır ama hiçbir yerde görünmez) yol açar; bu yüzden burada bilerek
        // filtreyi kapatıp silinmiş kişileri ayrıca işaretliyoruz.
        // Sıralama: önce kişiye atanmış fotoğraflar (ada göre), en sonda ilişkilendirilmemiş
        // olanlar — böylece bir kişinin fotoğrafları sayfa sınırında bölünse bile grup kartı
        // tutarlı bir sırada tekrar görünür.
        var orderedQuery = _context.PersonPhotos
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Include(p => p.Person)
            .OrderBy(p => p.PersonId == null ? 1 : 0)
            .ThenBy(p => p.PersonId == null ? string.Empty : (p.Person!.Ad + " " + p.Person.Soyad))
            .ThenByDescending(p => p.IsPrimary)
            .ThenBy(p => p.CreatedAt);

        var totalCount = await orderedQuery.CountAsync();

        if (photoId.HasValue)
        {
            var orderedIds = await orderedQuery.Select(p => p.Id).ToListAsync();
            var index = orderedIds.IndexOf(photoId.Value);
            if (index >= 0)
            {
                page = (index / pageSize) + 1;
            }
        }

        var pagePhotos = await orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var vm = new PhotoGalleryViewModel
        {
            TotalPhotoCount = totalCount,
            Page = page,
            PageSize = pageSize,
            Groups = pagePhotos
                .Where(p => p.PersonId.HasValue && p.Person != null)
                .GroupBy(p => p.PersonId!.Value)
                .Select(g => new PersonPhotoGroupViewModel
                {
                    PersonId = g.Key,
                    PersonName = $"{g.First().Person!.Ad} {g.First().Person!.Soyad}",
                    PersonIsDeleted = g.First().Person!.IsDeleted,
                    Photos = g.Select(ToViewModel).ToList(),
                })
                .OrderBy(g => g.PersonName)
                .ToList(),
            UnassignedPhotos = pagePhotos
                .Where(p => !p.PersonId.HasValue)
                .Select(ToViewModel)
                .ToList(),
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> Upload(List<IFormFile>? photos, int page = 1, int pageSize = DefaultPageSize)
    {
        if (photos == null || photos.Count == 0 || photos.All(f => f.Length == 0))
        {
            TempData["ErrorMessage"] = "Lütfen en az bir fotoğraf seçin.";
            return RedirectToAction(nameof(Index), new { page, pageSize });
        }

        var uploaded = 0;
        var errors = new List<string>();

        foreach (var file in photos.Where(f => f.Length > 0))
        {
            var result = await _photoService.SavePhotoAsync(null, file, isPrimary: false);
            if (result.Success)
            {
                uploaded++;
            }
            else
            {
                errors.Add($"{file.FileName}: {result.ErrorMessage}");
            }
        }

        if (uploaded > 0)
        {
            await _auditLogService.LogAsync($"İlişkilendirilmemiş {uploaded} fotoğraf yüklendi", "PersonPhoto");
            TempData["SuccessMessage"] = $"{uploaded} fotoğraf yüklendi.";
        }

        if (errors.Count > 0)
        {
            TempData["ErrorMessage"] = string.Join(" | ", errors);
        }

        return RedirectToAction(nameof(Index), new { page, pageSize });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> Delete(int photoId, int page = 1, int pageSize = DefaultPageSize)
    {
        var deleted = await _photoService.DeletePhotoAsync(photoId);
        if (deleted)
        {
            await _auditLogService.LogAsync("Fotoğraf silindi", "PersonPhoto", photoId);
            TempData["SuccessMessage"] = "Fotoğraf silindi.";
        }

        return RedirectToAction(nameof(Index), new { page, pageSize });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> Edit(int photoId, IFormFile file)
    {
        var result = await _photoService.ReplacePhotoFileAsync(photoId, file);
        if (result.Success)
        {
            await _auditLogService.LogAsync("Fotoğraf düzenlendi (kırpma/döndürme)", "PersonPhoto", photoId);
            return Ok(new { success = true, filePath = result.Photo!.FilePath });
        }

        return BadRequest(new { success = false, error = result.ErrorMessage });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> SetPrimary(int photoId, int personId, int page = 1, int pageSize = DefaultPageSize)
    {
        await _photoService.SetPrimaryAsync(personId, photoId);
        await _auditLogService.LogAsync("Ana fotoğraf değiştirildi", "Person", personId);
        TempData["SuccessMessage"] = "Ana fotoğraf güncellendi.";
        return RedirectToAction(nameof(Index), new { page, pageSize });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> Assign(int photoId, int personId, int page = 1, int pageSize = DefaultPageSize)
    {
        var assigned = await _photoService.AssignToPersonAsync(photoId, personId);
        if (assigned)
        {
            await _auditLogService.LogAsync("Fotoğraf kişiye atandı", "PersonPhoto", photoId);
            TempData["SuccessMessage"] = "Fotoğraf kişiye atandı.";
        }
        else
        {
            TempData["ErrorMessage"] = "Fotoğraf veya kişi bulunamadı.";
        }

        return RedirectToAction(nameof(Index), new { page, pageSize });
    }

    private static PersonPhotoViewModel ToViewModel(Models.PersonPhoto p) => new()
    {
        Id = p.Id,
        FilePath = p.FilePath,
        Description = p.Description,
        IsPrimary = p.IsPrimary,
    };
}
