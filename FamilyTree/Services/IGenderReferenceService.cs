using FamilyTree.Models;

namespace FamilyTree.Services;

public class GenderImportResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int Added { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public class GenderMatchResult
{
    /// <summary>Referansa göre cinsiyeti yeni doldurulan/değiştirilen kişi sayısı.</summary>
    public int Updated { get; set; }

    /// <summary>Referansta eşleşme bulunan ama cinsiyeti zaten aynı olan kişi sayısı.</summary>
    public int AlreadyCorrect { get; set; }

    /// <summary>Adı referans listesinde bulunamayan kişi sayısı.</summary>
    public int NoMatch { get; set; }
}

public interface IGenderReferenceService
{
    Task<int> GetReferenceCountAsync();

    Task<List<AdCinsiyet>> GetReferenceSampleAsync(int limit = 25);

    /// <summary>
    /// "Ad" ve "Cinsiyet" sütunlarını içeren bir .xlsx veya .csv dosyasını referans listesine
    /// aktarır (upsert: aynı ad varsa cinsiyeti güncellenir, yoksa eklenir).
    /// </summary>
    Task<GenderImportResult> ImportReferenceAsync(Stream fileStream, string extension);

    Task<int> ClearReferenceAsync();

    /// <summary>
    /// Referans listesine göre kişilerin cinsiyetini adlarına bakarak doldurur.
    /// <paramref name="overwriteExisting"/> false ise yalnızca cinsiyeti boş olan kişiler güncellenir.
    /// </summary>
    Task<GenderMatchResult> MatchByNamesAsync(bool overwriteExisting);

    Task<(List<Person> Persons, int TotalCount)> GetMissingGenderAsync(int page, int pageSize);

    /// <summary>Verilen kişi Id → Cinsiyet eşlemelerini toplu uygular. Güncellenen kişi sayısını döndürür.</summary>
    Task<int> BulkSetGendersAsync(IDictionary<int, Gender> assignments);
}
