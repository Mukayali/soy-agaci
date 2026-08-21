using FamilyTree.Data;
using FamilyTree.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FamilyTree.Controllers;

public class FamilyTreeController : Controller
{
    private readonly ApplicationDbContext _context;

    public FamilyTreeController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(int? id)
    {
        if (id == null)
        {
            var first = await _context.Persons.AsNoTracking().OrderBy(p => p.Id).Select(p => p.Id).FirstOrDefaultAsync();
            if (first == 0)
            {
                ViewBag.NoPersons = true;
                return View();
            }

            return RedirectToAction(nameof(Index), new { id = first });
        }

        var person = await _context.Persons.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (person == null)
        {
            return NotFound();
        }

        ViewBag.PersonId = person.Id;
        ViewBag.PersonName = $"{person.Ad} {person.Soyad}";
        await PopulateSulalelerAsync();

        return View();
    }

    public async Task<IActionResult> Sulale(int id)
    {
        var sulale = await _context.Sulaleler.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        if (sulale == null)
        {
            return NotFound();
        }

        ViewBag.SulaleId = sulale.Id;
        ViewBag.SulaleAdi = sulale.Ad;
        await PopulateSulalelerAsync();

        return View("Index");
    }

    private async Task PopulateSulalelerAsync()
    {
        ViewBag.Sulaleler = await _context.Sulaleler
            .AsNoTracking()
            .OrderBy(s => s.Ad)
            .Select(s => new SulaleListItemViewModel { Id = s.Id, Ad = s.Ad })
            .ToListAsync();
    }
}
