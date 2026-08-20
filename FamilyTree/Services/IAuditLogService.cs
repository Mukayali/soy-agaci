namespace FamilyTree.Services;

public interface IAuditLogService
{
    /// <summary>
    /// Bir işlemi audit log'a kaydeder. TC Kimlik No, şifre veya başka hassas kişisel
    /// veri asla `islem` metnine dahil edilmemelidir.
    /// </summary>
    Task LogAsync(string islem, string entity, int? entityId = null);
}
