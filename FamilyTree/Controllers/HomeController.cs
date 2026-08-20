using System.Diagnostics;
using FamilyTree.Data;
using FamilyTree.Models;
using FamilyTree.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FamilyTree.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ApplicationDbContext _context;

    public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var vm = new HomeViewModel
        {
            ToplamKisi = await _context.Persons.CountAsync(),
            ToplamFotograf = await _context.PersonPhotos.CountAsync(),
            SonEklenenler = await _context.Persons
                .AsNoTracking()
                .OrderByDescending(p => p.CreatedAt)
                .Take(6)
                .Select(p => new PersonListItemViewModel
                {
                    Id = p.Id,
                    AdSoyad = p.Ad + " " + p.Soyad,
                    DogumTarihi = p.DogumTarihi,
                    OlumTarihi = p.OlumTarihi,
                })
                .ToListAsync(),
        };

        return View(vm);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
