using FamilyTree.Data;
using FamilyTree.Models;
using FamilyTree.Services;
using FamilyTree.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FamilyTree.Controllers;

public class PersonController : Controller
{
    private readonly IPersonService _personService;
    private readonly IPhotoService _photoService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PersonController> _logger;

    public PersonController(
        IPersonService personService,
        IPhotoService photoService,
        ApplicationDbContext context,
        ILogger<PersonController> logger)
    {
        _personService = personService;
        _photoService = photoService;
        _context = context;
        _logger = logger;
    }

    public async Task<IActionResult> Index(string? q, int page = 1)
    {
        var result = await _personService.SearchAsync(q, page);
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

    public IActionResult Create()
    {
        return View(new PersonCreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PersonCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var (success, id, errorMessage) = await _personService.CreateAsync(model);

        if (!success)
        {
            ModelState.AddModelError(string.Empty, errorMessage ?? "Kişi kaydedilemedi.");
            return View(model);
        }

        TempData["SuccessMessage"] = "Kişi başarıyla eklendi.";
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var vm = await _personService.GetForEditAsync(id);
        if (vm == null)
        {
            return NotFound();
        }

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PersonEditViewModel model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var (success, errorMessage) = await _personService.UpdateAsync(model);

        if (!success)
        {
            ModelState.AddModelError(string.Empty, errorMessage ?? "Kişi güncellenemedi.");
            return View(model);
        }

        TempData["SuccessMessage"] = "Kişi başarıyla güncellendi.";
        return RedirectToAction(nameof(Details), new { id });
    }

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
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        await _personService.DeleteAsync(id);
        TempData["SuccessMessage"] = "Kişi silindi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePhoto(int photoId, int personId)
    {
        await _photoService.DeletePhotoAsync(photoId);
        return RedirectToAction(nameof(Edit), new { id = personId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPrimaryPhoto(int photoId, int personId)
    {
        await _photoService.SetPrimaryAsync(personId, photoId);
        return RedirectToAction(nameof(Edit), new { id = personId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
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
            }
        }

        return RedirectToAction(nameof(Details), new { id = personId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveSpouse(int spouseRelationshipId, int personId)
    {
        var relationship = await _context.SpouseRelationships.FindAsync(spouseRelationshipId);
        if (relationship != null)
        {
            _context.SpouseRelationships.Remove(relationship);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Details), new { id = personId });
    }
}
