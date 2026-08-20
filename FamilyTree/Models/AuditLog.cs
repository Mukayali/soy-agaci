namespace FamilyTree.Models;

public class AuditLog
{
    public int Id { get; set; }

    public string? UserId { get; set; }

    public string? KullaniciAdi { get; set; }

    public string Islem { get; set; } = string.Empty;

    public string Entity { get; set; } = string.Empty;

    public int? EntityId { get; set; }

    public string? Ip { get; set; }

    public DateTime Tarih { get; set; } = DateTime.UtcNow;
}
