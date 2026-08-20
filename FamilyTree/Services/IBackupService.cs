namespace FamilyTree.Services;

public class BackupResult
{
    public bool Success { get; set; }
    public byte[]? Data { get; set; }
    public string? FileName { get; set; }
    public string? ErrorMessage { get; set; }
}

public interface IBackupService
{
    /// <summary>
    /// `mysqldump` çalıştırarak veritabanının tam bir SQL yedeğini alır.
    /// Sunucuda `mysqldump` bulunamazsa veya işlem başarısız olursa Success=false döner.
    /// </summary>
    Task<BackupResult> CreateBackupAsync();
}
