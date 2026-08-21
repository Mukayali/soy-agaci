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
