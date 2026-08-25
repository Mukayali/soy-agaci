namespace FamilyTree.Services;

public class CsvImportResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int PersonsCreated { get; set; }
    public int PersonsUpdated { get; set; }
    public int RelationshipsLinked { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public interface ICsvImportService
{
    /// <summary>
    /// TC Kimlik No, Ad, Soyad, Cinsiyet, DogumTarihi, OlumTarihi, AnneTC, BabaTC sütunlarını
    /// içeren bir CSV dosyasını içe aktarır. Bir satırın TC'si veritabanında zaten aktif bir
    /// kişiye aitse yeni kişi oluşturulmaz; bunun yerine o kişi satırdaki dolu alanlarla
    /// güncellenir (upsert) — boş bırakılan alanlar mevcut değeri korur, üzerine yazmaz.
    /// Anne/Baba ilişkileri AnneTC/BabaTC sütunlarındaki TC Kimlik No üzerinden hem CSV
    /// içindeki diğer satırlara hem de veritabanında zaten kayıtlı kişilere bağlanır (bu da
    /// güncellenen satırlar için geçerlidir). Tüm işlem tek bir transaction içinde yapılır.
    /// </summary>
    Task<CsvImportResult> ImportAsync(Stream csvContent);
}
