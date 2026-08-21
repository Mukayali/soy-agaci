namespace FamilyTree.Services;

public class GedcomImportResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int PersonsCreated { get; set; }
    public int FamiliesProcessed { get; set; }
    public int SpouseRelationshipsCreated { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public interface IGedcomService
{
    /// <summary>Sistemdeki tüm kişileri ve aile ilişkilerini GEDCOM 5.5.1 formatında dışa aktarır.</summary>
    Task<byte[]> ExportAsync();

    /// <summary>
    /// Bir GEDCOM (.ged) dosyasını içe aktarır: her INDI kaydı yeni bir Person, her FAM kaydı
    /// anne/baba ilişkileri ve (varsa) bir SpouseRelationship oluşturur. Tüm işlem tek bir
    /// transaction içinde yapılır; bir hata oluşursa hiçbir kayıt eklenmez.
    /// </summary>
    Task<GedcomImportResult> ImportAsync(Stream gedcomContent);
}
