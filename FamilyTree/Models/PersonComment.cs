using System.ComponentModel.DataAnnotations;

namespace FamilyTree.Models;

public class PersonComment
{
    public int Id { get; set; }

    public int PersonId { get; set; }

    /// <summary>ASP.NET Identity kullanıcı Id'si. Kullanıcı sonradan silinirse null kalabilir
    /// (AuditLog'daki UserId/KullaniciAdi desenine benzer şekilde FK zorunlu tutulmaz),
    /// yorum ve o anki kullanıcı adı anlık görüntüsü (KullaniciAdi) korunur.</summary>
    public string? UserId { get; set; }

    [Required]
    [MaxLength(256)]
    public string KullaniciAdi { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Yorum { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Person Person { get; set; } = null!;
}
