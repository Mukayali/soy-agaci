using FamilyTree.Data;
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

        return View();
    }
}
