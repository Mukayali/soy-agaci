namespace FamilyTree.ViewModels;

public class AdminCommentItemViewModel
{
    public int Id { get; set; }
    public int PersonId { get; set; }
    public string PersonAdSoyad { get; set; } = string.Empty;
    public string KullaniciAdi { get; set; } = string.Empty;
    public string Yorum { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class AdminCommentListViewModel
{
    public List<AdminCommentItemViewModel> Items { get; set; } = new();
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}
