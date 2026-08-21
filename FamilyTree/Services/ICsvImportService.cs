namespace FamilyTree.Services;

public class CsvImportResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int PersonsCreated { get; set; }
    public int RelationshipsLinked { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public interface ICsvImportService
{
    /// <summary>
    /// TC Kimlik No, Ad, Soyad, Cinsiyet, DogumTarihi, OlumTarihi, AnneTC, BabaTC sütunlarını
    /// içeren bir CSV dosyasını içe aktarır. Anne/Baba ilişkileri AnneTC/BabaTC sütunlarındaki
    /// TC Kimlik No üzerinden hem CSV içindeki diğer satırlara hem de veritabanında zaten
    /// kayıtlı kişilere bağlanır. Tüm işlem tek bir transaction içinde yapılır.
    /// </summary>
    Task<CsvImportResult> ImportAsync(Stream csvContent);
}
