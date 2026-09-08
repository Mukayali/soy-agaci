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
        var today = DateTime.Today;

        var doganlar = await _context.Persons
            .AsNoTracking()
            .Include(p => p.Photos.Where(ph => ph.IsPrimary))
            .Where(p => p.DogumTarihi.HasValue
                && p.DogumTarihi.Value.Month == today.Month
                && p.DogumTarihi.Value.Day == today.Day)
            .OrderBy(p => p.Ad).ThenBy(p => p.Soyad)
            .ToListAsync();

        var kaybettiklerimiz = await _context.Persons
            .AsNoTracking()
            .Include(p => p.Photos.Where(ph => ph.IsPrimary))
            .Where(p => p.OlumTarihi.HasValue
                && p.OlumTarihi.Value.Month == today.Month
                && p.OlumTarihi.Value.Day == today.Day)
            .OrderBy(p => p.Ad).ThenBy(p => p.Soyad)
            .ToListAsync();

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
            BugunDoganlar = doganlar.Select(p => new BirthdayCardViewModel
            {
                Id = p.Id,
                AdSoyad = $"{p.Ad} {p.Soyad}",
                DogumYili = p.DogumTarihi!.Value.Year,
                Yas = today.Year - p.DogumTarihi.Value.Year,
                PrimaryPhotoPath = p.Photos.FirstOrDefault()?.FilePath,
            }).ToList(),
            BugunKaybettiklerimiz = kaybettiklerimiz.Select(p => new BirthdayCardViewModel
            {
                Id = p.Id,
                AdSoyad = $"{p.Ad} {p.Soyad}",
                DogumYili = p.DogumTarihi?.Year,
                Yas = p.DogumTarihi.HasValue ? CalculateAge(p.DogumTarihi.Value, p.OlumTarihi!.Value) : null,
                PrimaryPhotoPath = p.Photos.FirstOrDefault()?.FilePath,
            }).ToList(),
        };

        return View(vm);
    }

    private static int CalculateAge(DateTime dogum, DateTime asOf)
    {
        var age = asOf.Year - dogum.Year;
        if (dogum.Month > asOf.Month || (dogum.Month == asOf.Month && dogum.Day > asOf.Day))
        {
            age--;
        }

        return age;
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
