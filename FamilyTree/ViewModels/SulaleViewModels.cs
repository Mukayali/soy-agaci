using System.ComponentModel.DataAnnotations;

namespace FamilyTree.ViewModels;

public class SulaleListItemViewModel
{
    public int Id { get; set; }
    public string Ad { get; set; } = string.Empty;
    public string? Aciklama { get; set; }
    public int UyeSayisi { get; set; }
}

public class SulaleCreateViewModel
{
    [Required(ErrorMessage = "Sülale adı zorunludur.")]
    [MaxLength(150)]
    [Display(Name = "Sülale Adı")]
    public string Ad { get; set; } = string.Empty;

    [MaxLength(500)]
    [Display(Name = "Açıklama")]
    public string? Aciklama { get; set; }
}

public class SulaleEditViewModel : SulaleCreateViewModel
{
    public int Id { get; set; }
}

public class BulkSulaleViewModel
{
    public string? Query { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

    /// <summary>Arama yapıldıysa true; ilk açılışta (boş sorgu) liste gösterilmez.</summary>
    public bool Searched { get; set; }

    public List<PersonListItemViewModel> Persons { get; set; } = new();
    public List<SulaleListItemViewModel> Sulaleler { get; set; } = new();
}
