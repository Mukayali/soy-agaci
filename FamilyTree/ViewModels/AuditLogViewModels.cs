namespace FamilyTree.ViewModels;

public class AuditLogListViewModel
{
    public List<AuditLogItemViewModel> Items { get; set; } = new();
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public class AuditLogItemViewModel
{
    public int Id { get; set; }
    public string? KullaniciAdi { get; set; }
    public string Islem { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
    public int? EntityId { get; set; }
    public string? Ip { get; set; }
    public DateTime Tarih { get; set; }
}
