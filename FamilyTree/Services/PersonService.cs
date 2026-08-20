using FamilyTree.Data;
using FamilyTree.Models;
using FamilyTree.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace FamilyTree.Services;

public class PersonService : IPersonService
{
    private const int MaxAncestorDepth = 40;

    private readonly ApplicationDbContext _context;
    private readonly IPhotoService _photoService;

    public PersonService(ApplicationDbContext context, IPhotoService photoService)
    {
        _context = context;
        _photoService = photoService;
    }

    public async Task<PersonDetailViewModel?> GetDetailsAsync(int id)
    {
        var person = await _context.Persons
            .AsNoTracking()
            .Include(p => p.Anne).ThenInclude(a => a!.Anne)
            .Include(p => p.Anne).ThenInclude(a => a!.Baba)
            .Include(p => p.Baba).ThenInclude(b => b!.Anne)
            .Include(p => p.Baba).ThenInclude(b => b!.Baba)
            .Include(p => p.Photos)
            .Include(p => p.SpouseRelationshipsAsPerson1).ThenInclude(sr => sr.Person2)
            .Include(p => p.SpouseRelationshipsAsPerson2).ThenInclude(sr => sr.Person1)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (person == null)
        {
            return null;
        }

        var children = await _context.Persons
            .AsNoTracking()
            .Where(p => p.AnneId == id || p.BabaId == id)
            .OrderBy(p => p.DogumTarihi)
            .ToListAsync();

        var siblings = new List<Person>();
        if (person.AnneId.HasValue || person.BabaId.HasValue)
        {
            siblings = await _context.Persons
                .AsNoTracking()
                .Where(p => p.Id != id &&
                    ((person.AnneId.HasValue && p.AnneId == person.AnneId) ||
                     (person.BabaId.HasValue && p.BabaId == person.BabaId)))
                .OrderBy(p => p.DogumTarihi)
                .ToListAsync();
        }

        var childIds = children.Select(c => c.Id).ToList();
        var grandchildren = childIds.Count == 0
            ? new List<Person>()
            : await _context.Persons
                .AsNoTracking()
                .Where(p => (p.AnneId.HasValue && childIds.Contains(p.AnneId.Value)) ||
                            (p.BabaId.HasValue && childIds.Contains(p.BabaId.Value)))
                .OrderBy(p => p.DogumTarihi)
                .ToListAsync();

        var siblingIds = siblings.Select(s => s.Id).ToList();
        var nieces = siblingIds.Count == 0
            ? new List<Person>()
            : await _context.Persons
                .AsNoTracking()
                .Where(p => (p.AnneId.HasValue && siblingIds.Contains(p.AnneId.Value)) ||
                            (p.BabaId.HasValue && siblingIds.Contains(p.BabaId.Value)))
                .OrderBy(p => p.DogumTarihi)
                .ToListAsync();

        var spouses = person.SpouseRelationshipsAsPerson1.Select(sr => sr.Person2)
            .Concat(person.SpouseRelationshipsAsPerson2.Select(sr => sr.Person1))
            .ToList();

        var buyukebeveynler = new List<PersonListItemViewModel>();
        if (person.Anne?.Anne != null)
        {
            buyukebeveynler.Add(ToListItem(person.Anne.Anne, "Nine (Anne tarafı)"));
        }

        if (person.Anne?.Baba != null)
        {
            buyukebeveynler.Add(ToListItem(person.Anne.Baba, "Dede (Anne tarafı)"));
        }

        if (person.Baba?.Anne != null)
        {
            buyukebeveynler.Add(ToListItem(person.Baba.Anne, "Nine (Baba tarafı)"));
        }

        if (person.Baba?.Baba != null)
        {
            buyukebeveynler.Add(ToListItem(person.Baba.Baba, "Dede (Baba tarafı)"));
        }

        var anneninAnneId = person.Anne?.AnneId;
        var anneninBabaId = person.Anne?.BabaId;
        var anneKardesleri = person.Anne == null || (anneninAnneId == null && anneninBabaId == null)
            ? new List<Person>()
            : await _context.Persons.AsNoTracking()
                .Where(p => p.Id != person.AnneId &&
                    ((anneninAnneId.HasValue && p.AnneId == anneninAnneId) ||
                     (anneninBabaId.HasValue && p.BabaId == anneninBabaId)))
                .ToListAsync();

        var babaninAnneId = person.Baba?.AnneId;
        var babaninBabaId = person.Baba?.BabaId;
        var babaKardesleri = person.Baba == null || (babaninAnneId == null && babaninBabaId == null)
            ? new List<Person>()
            : await _context.Persons.AsNoTracking()
                .Where(p => p.Id != person.BabaId &&
                    ((babaninAnneId.HasValue && p.AnneId == babaninAnneId) ||
                     (babaninBabaId.HasValue && p.BabaId == babaninBabaId)))
                .ToListAsync();

        var dayilar = anneKardesleri.Where(p => p.Cinsiyet == Gender.Erkek).ToList();
        var teyzeler = anneKardesleri.Where(p => p.Cinsiyet == Gender.Kadin).ToList();
        var amcalar = babaKardesleri.Where(p => p.Cinsiyet == Gender.Erkek).ToList();
        var halalar = babaKardesleri.Where(p => p.Cinsiyet == Gender.Kadin).ToList();

        var ebeveynKardesIds = anneKardesleri.Concat(babaKardesleri).Select(p => p.Id).Distinct().ToList();
        var kuzenler = ebeveynKardesIds.Count == 0
            ? new List<Person>()
            : await _context.Persons.AsNoTracking()
                .Where(p => (p.AnneId.HasValue && ebeveynKardesIds.Contains(p.AnneId.Value)) ||
                            (p.BabaId.HasValue && ebeveynKardesIds.Contains(p.BabaId.Value)))
                .OrderBy(p => p.DogumTarihi)
                .ToListAsync();

        var vm = new PersonDetailViewModel
        {
            Id = person.Id,
            Ad = person.Ad,
            Soyad = person.Soyad,
            TcKimlikNoMasked = MaskTc(person.TcKimlikNo),
            DogumTarihi = person.DogumTarihi,
            OlumTarihi = person.OlumTarihi,
            Aciklama = person.Aciklama,
            Anne = person.Anne == null ? null : ToListItem(person.Anne),
            Baba = person.Baba == null ? null : ToListItem(person.Baba),
            Esler = spouses.Select(p => ToListItem(p)).ToList(),
            Cocuklar = children.Select(p => ToListItem(p)).ToList(),
            Kardesler = siblings.Select(p => ToListItem(p)).ToList(),
            Torunlar = grandchildren.Select(p => ToListItem(p)).ToList(),
            Yegenler = nieces.Select(p => ToListItem(p)).ToList(),
            BuyukebeveynLer = buyukebeveynler,
            Amcalar = amcalar.Select(p => ToListItem(p)).ToList(),
            Dayilar = dayilar.Select(p => ToListItem(p)).ToList(),
            Halalar = halalar.Select(p => ToListItem(p)).ToList(),
            Teyzeler = teyzeler.Select(p => ToListItem(p)).ToList(),
            Kuzenler = kuzenler.Select(p => ToListItem(p)).ToList(),
            Photos = person.Photos
                .OrderByDescending(p => p.IsPrimary)
                .ThenBy(p => p.CreatedAt)
                .Select(p => new PersonPhotoViewModel
                {
                    Id = p.Id,
                    FilePath = p.FilePath,
                    Description = p.Description,
                    IsPrimary = p.IsPrimary,
                })
                .ToList(),
        };

        return vm;
    }

    public async Task<PersonEditViewModel?> GetForEditAsync(int id)
    {
        var person = await _context.Persons
            .AsNoTracking()
            .Include(p => p.Photos)
            .Include(p => p.Anne)
            .Include(p => p.Baba)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (person == null)
        {
            return null;
        }

        return new PersonEditViewModel
        {
            Id = person.Id,
            Ad = person.Ad,
            Soyad = person.Soyad,
            TcKimlikNo = person.TcKimlikNo,
            DogumTarihi = person.DogumTarihi,
            OlumTarihi = person.OlumTarihi,
            Cinsiyet = person.Cinsiyet,
            AnneId = person.AnneId,
            BabaId = person.BabaId,
            AnneAdSoyad = person.Anne == null ? null : $"{person.Anne.Ad} {person.Anne.Soyad}",
            BabaAdSoyad = person.Baba == null ? null : $"{person.Baba.Ad} {person.Baba.Soyad}",
            Aciklama = person.Aciklama,
            ExistingPhotos = person.Photos
                .OrderByDescending(p => p.IsPrimary)
                .ThenBy(p => p.CreatedAt)
                .Select(p => new PersonPhotoViewModel
                {
                    Id = p.Id,
                    FilePath = p.FilePath,
                    Description = p.Description,
                    IsPrimary = p.IsPrimary,
                })
                .ToList(),
        };
    }

    public async Task<PersonIndexViewModel> SearchAsync(string? query, int page = 1, int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var baseQuery = _context.Persons.AsNoTracking().Include(p => p.Anne).Include(p => p.Baba).AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var trimmed = query.Trim();
            baseQuery = baseQuery.Where(p =>
                EF.Functions.Like(p.Ad, $"%{trimmed}%") ||
                EF.Functions.Like(p.Soyad, $"%{trimmed}%") ||
                EF.Functions.Like((p.Ad + " " + p.Soyad), $"%{trimmed}%") ||
                (p.TcKimlikNo != null && p.TcKimlikNo == trimmed));
        }

        var totalCount = await baseQuery.CountAsync();

        var persons = await baseQuery
            .OrderBy(p => p.Ad).ThenBy(p => p.Soyad)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PersonIndexViewModel
        {
            Persons = persons.Select(p => ToListItem(p)).ToList(),
            Query = query,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<List<PersonSearchResultItem>> QuickSearchAsync(string query, int? excludePersonId = null, int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
        {
            return new List<PersonSearchResultItem>();
        }

        var trimmed = query.Trim();

        var results = await _context.Persons
            .AsNoTracking()
            .Where(p => (excludePersonId == null || p.Id != excludePersonId) &&
                (EF.Functions.Like(p.Ad, $"%{trimmed}%") ||
                 EF.Functions.Like(p.Soyad, $"%{trimmed}%") ||
                 EF.Functions.Like((p.Ad + " " + p.Soyad), $"%{trimmed}%")))
            .OrderBy(p => p.Ad).ThenBy(p => p.Soyad)
            .Take(limit)
            .Select(p => new PersonSearchResultItem
            {
                Id = p.Id,
                AdSoyad = p.Ad + " " + p.Soyad,
                DogumYili = p.DogumTarihi.HasValue ? p.DogumTarihi.Value.Year.ToString() : null,
                OlumYili = p.OlumTarihi.HasValue ? p.OlumTarihi.Value.Year.ToString() : null,
            })
            .ToListAsync();

        return results;
    }

    public async Task<(bool Success, int? Id, string? ErrorMessage)> CreateAsync(PersonCreateViewModel model)
    {
        var validation = await ValidateRelationshipAsync(null, model.AnneId, model.BabaId);
        if (!validation.IsValid)
        {
            return (false, null, validation.ErrorMessage);
        }

        if (!string.IsNullOrWhiteSpace(model.TcKimlikNo))
        {
            var exists = await _context.Persons.AnyAsync(p => p.TcKimlikNo == model.TcKimlikNo);
            if (exists)
            {
                return (false, null, "Bu TC Kimlik Numarası ile kayıtlı başka bir kişi bulunuyor.");
            }
        }

        var person = new Person
        {
            Ad = model.Ad.Trim(),
            Soyad = model.Soyad.Trim(),
            TcKimlikNo = string.IsNullOrWhiteSpace(model.TcKimlikNo) ? null : model.TcKimlikNo.Trim(),
            DogumTarihi = model.DogumTarihi,
            OlumTarihi = model.OlumTarihi,
            Cinsiyet = model.Cinsiyet,
            AnneId = model.AnneId,
            BabaId = model.BabaId,
            Aciklama = model.Aciklama,
            CreatedAt = DateTime.UtcNow,
        };

        _context.Persons.Add(person);
        await _context.SaveChangesAsync();

        if (model.Photos != null)
        {
            foreach (var file in model.Photos.Where(f => f.Length > 0))
            {
                await _photoService.SavePhotoAsync(person.Id, file, isPrimary: false);
            }
        }

        return (true, person.Id, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateAsync(PersonEditViewModel model)
    {
        var person = await _context.Persons.FirstOrDefaultAsync(p => p.Id == model.Id);
        if (person == null)
        {
            return (false, "Kişi bulunamadı.");
        }

        var validation = await ValidateRelationshipAsync(model.Id, model.AnneId, model.BabaId);
        if (!validation.IsValid)
        {
            return (false, validation.ErrorMessage);
        }

        if (!string.IsNullOrWhiteSpace(model.TcKimlikNo))
        {
            var exists = await _context.Persons.AnyAsync(p => p.TcKimlikNo == model.TcKimlikNo && p.Id != model.Id);
            if (exists)
            {
                return (false, "Bu TC Kimlik Numarası ile kayıtlı başka bir kişi bulunuyor.");
            }
        }

        person.Ad = model.Ad.Trim();
        person.Soyad = model.Soyad.Trim();
        person.TcKimlikNo = string.IsNullOrWhiteSpace(model.TcKimlikNo) ? null : model.TcKimlikNo.Trim();
        person.DogumTarihi = model.DogumTarihi;
        person.OlumTarihi = model.OlumTarihi;
        person.Cinsiyet = model.Cinsiyet;
        person.AnneId = model.AnneId;
        person.BabaId = model.BabaId;
        person.Aciklama = model.Aciklama;
        person.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        if (model.Photos != null)
        {
            foreach (var file in model.Photos.Where(f => f.Length > 0))
            {
                await _photoService.SavePhotoAsync(person.Id, file, isPrimary: false);
            }
        }

        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage, int ChildCount, int SpouseCount)> GetDeleteInfoAsync(int id)
    {
        var person = await _context.Persons.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (person == null)
        {
            return (false, "Kişi bulunamadı.", 0, 0);
        }

        var childCount = await _context.Persons.CountAsync(p => p.AnneId == id || p.BabaId == id);
        var spouseCount = await _context.SpouseRelationships.CountAsync(sr => sr.Person1Id == id || sr.Person2Id == id);

        return (true, null, childCount, spouseCount);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var person = await _context.Persons.FirstOrDefaultAsync(p => p.Id == id);
        if (person == null)
        {
            return false;
        }

        person.IsDeleted = true;
        person.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return true;
    }

    private async Task<(bool IsValid, string? ErrorMessage)> ValidateRelationshipAsync(int? personId, int? anneId, int? babaId)
    {
        if (anneId.HasValue && babaId.HasValue && anneId.Value == babaId.Value)
        {
            return (false, "Anne ve baba aynı kişi olamaz.");
        }

        if (anneId.HasValue)
        {
            var anneExists = await _context.Persons.AnyAsync(p => p.Id == anneId.Value);
            if (!anneExists)
            {
                return (false, "Seçilen anne bulunamadı.");
            }
        }

        if (babaId.HasValue)
        {
            var babaExists = await _context.Persons.AnyAsync(p => p.Id == babaId.Value);
            if (!babaExists)
            {
                return (false, "Seçilen baba bulunamadı.");
            }
        }

        if (personId.HasValue)
        {
            if (anneId == personId.Value || babaId == personId.Value)
            {
                return (false, "Kişi kendisinin annesi veya babası olamaz.");
            }

            if (anneId.HasValue)
            {
                var anneAncestors = await GetAncestorIdsAsync(anneId.Value);
                if (anneAncestors.Contains(personId.Value))
                {
                    return (false, "Bu ilişki soy ağacında bir döngü oluşturur.");
                }
            }

            if (babaId.HasValue)
            {
                var babaAncestors = await GetAncestorIdsAsync(babaId.Value);
                if (babaAncestors.Contains(personId.Value))
                {
                    return (false, "Bu ilişki soy ağacında bir döngü oluşturur.");
                }
            }
        }

        return (true, null);
    }

    private async Task<HashSet<int>> GetAncestorIdsAsync(int startPersonId)
    {
        var visited = new HashSet<int>();
        var frontier = new List<int> { startPersonId };
        var depth = 0;

        while (frontier.Count > 0 && depth < MaxAncestorDepth)
        {
            var parents = await _context.Persons
                .AsNoTracking()
                .Where(p => frontier.Contains(p.Id))
                .Select(p => new { p.AnneId, p.BabaId })
                .ToListAsync();

            var nextFrontier = new List<int>();
            foreach (var p in parents)
            {
                if (p.AnneId.HasValue && visited.Add(p.AnneId.Value))
                {
                    nextFrontier.Add(p.AnneId.Value);
                }

                if (p.BabaId.HasValue && visited.Add(p.BabaId.Value))
                {
                    nextFrontier.Add(p.BabaId.Value);
                }
            }

            frontier = nextFrontier;
            depth++;
        }

        return visited;
    }

    private static PersonListItemViewModel ToListItem(Person p, string? rol = null) => new()
    {
        Id = p.Id,
        AdSoyad = $"{p.Ad} {p.Soyad}",
        DogumTarihi = p.DogumTarihi,
        OlumTarihi = p.OlumTarihi,
        AnneAdSoyad = p.Anne == null ? null : $"{p.Anne.Ad} {p.Anne.Soyad}",
        BabaAdSoyad = p.Baba == null ? null : $"{p.Baba.Ad} {p.Baba.Soyad}",
        Rol = rol,
    };

    private static string? MaskTc(string? tc)
    {
        if (string.IsNullOrWhiteSpace(tc) || tc.Length != 11)
        {
            return null;
        }

        return $"{tc[..3]}*****{tc[^3..]}";
    }
}
