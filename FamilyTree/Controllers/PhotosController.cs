using FamilyTree.Data;
using FamilyTree.Services;
using FamilyTree.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FamilyTree.Controllers;

public class PhotosController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IPhotoService _photoService;
    private readonly IAuditLogService _auditLogService;

    public PhotosController(ApplicationDbContext context, IPhotoService photoService, IAuditLogService auditLogService)
    {
        _context = context;
        _photoService = photoService;
        _auditLogService = auditLogService;
    }

    public async Task<IActionResult> Index()
    {
        // IgnoreQueryFilters: Person üzerindeki "silinmemiş" global filtresi normalde Include
        // edilen gezinmelere de uygulanır. Bu, yumuşak silinmiş bir kişiye ait fotoğrafların
        // Person'ı sessizce null gelip hem grup hem "ilişkilendirilmemiş" listesinden düşmesine
        // (fotoğraf sayılır ama hiçbir yerde görünmez) yol açar; bu yüzden burada bilerek
        // filtreyi kapatıp silinmiş kişileri ayrıca işaretliyoruz.
        var photos = await _context.PersonPhotos
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Include(p => p.Person)
            .OrderByDescending(p => p.IsPrimary)
            .ThenBy(p => p.CreatedAt)
            .ToListAsync();

        var vm = new PhotoGalleryViewModel
        {
            TotalPhotoCount = photos.Count,
            Groups = photos
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
            UnassignedPhotos = photos
                .Where(p => !p.PersonId.HasValue)
                .Select(ToViewModel)
                .ToList(),
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> Upload(List<IFormFile>? photos)
    {
        if (photos == null || photos.Count == 0 || photos.All(f => f.Length == 0))
        {
            TempData["ErrorMessage"] = "Lütfen en az bir fotoğraf seçin.";
            return RedirectToAction(nameof(Index));
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

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> Delete(int photoId)
    {
        var deleted = await _photoService.DeletePhotoAsync(photoId);
        if (deleted)
        {
            await _auditLogService.LogAsync("Fotoğraf silindi", "PersonPhoto", photoId);
            TempData["SuccessMessage"] = "Fotoğraf silindi.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> Assign(int photoId, int personId)
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

        return RedirectToAction(nameof(Index));
    }

    private static PersonPhotoViewModel ToViewModel(Models.PersonPhoto p) => new()
    {
        Id = p.Id,
        FilePath = p.FilePath,
        Description = p.Description,
        IsPrimary = p.IsPrimary,
    };
}
