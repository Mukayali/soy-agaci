using System.ComponentModel.DataAnnotations;
using FamilyTree.Models;
using Microsoft.AspNetCore.Http;

namespace FamilyTree.ViewModels;

public class PersonListItemViewModel
{
    public int Id { get; set; }
    public string AdSoyad { get; set; } = string.Empty;
    public DateTime? DogumTarihi { get; set; }
    public DateTime? OlumTarihi { get; set; }
    public string? AnneAdSoyad { get; set; }
    public string? BabaAdSoyad { get; set; }
    public string? PrimaryPhotoPath { get; set; }
    public int? SulaleId { get; set; }
    public string? SulaleAdi { get; set; }

    /// <summary>Bu kişinin merkez kişiye göre akrabalık etiketi (ör. "Dede (Anne tarafı)"). Sadece bazı listelerde kullanılır.</summary>
    public string? Rol { get; set; }
}

public class PersonIndexViewModel
{
    public List<PersonListItemViewModel> Persons { get; set; } = new();
    public string? Query { get; set; }
    public int? SulaleId { get; set; }
    public string? SulaleAdi { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public class PersonSearchResultItem
{
    public int Id { get; set; }
    public string AdSoyad { get; set; } = string.Empty;
    public string? DogumYili { get; set; }
    public string? OlumYili { get; set; }
}

public class PersonCreateViewModel : IValidatableObject
{
    [Required(ErrorMessage = "Ad alanı zorunludur.")]
    [MaxLength(100)]
    [Display(Name = "Ad")]
    public string Ad { get; set; } = string.Empty;

    [Required(ErrorMessage = "Soyad alanı zorunludur.")]
    [MaxLength(100)]
    [Display(Name = "Soyad")]
    public string Soyad { get; set; } = string.Empty;

    [Display(Name = "TC Kimlik No")]
    [RegularExpression(@"^\d{11}$", ErrorMessage = "TC Kimlik No 11 haneli olmalıdır.")]
    public string? TcKimlikNo { get; set; }

    [Display(Name = "Doğum Tarihi")]
    [DataType(DataType.Date)]
    public DateTime? DogumTarihi { get; set; }

    [Display(Name = "Ölüm Tarihi")]
    [DataType(DataType.Date)]
    public DateTime? OlumTarihi { get; set; }

    [Display(Name = "Cinsiyet")]
    public Gender? Cinsiyet { get; set; }

    [Display(Name = "Doğum Yeri")]
    [MaxLength(200)]
    public string? DogumYeri { get; set; }

    [Display(Name = "Sülale")]
    public int? SulaleId { get; set; }

    [Display(Name = "Anne")]
    public int? AnneId { get; set; }

    [Display(Name = "Baba")]
    public int? BabaId { get; set; }

    [Display(Name = "Açıklama")]
    public string? Aciklama { get; set; }

    [Display(Name = "Fotoğraflar")]
    public List<IFormFile>? Photos { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DogumTarihi.HasValue && OlumTarihi.HasValue && DogumTarihi.Value > OlumTarihi.Value)
        {
            yield return new ValidationResult(
                "Doğum tarihi ölüm tarihinden sonra olamaz.",
                new[] { nameof(DogumTarihi), nameof(OlumTarihi) });
        }

        if (OlumTarihi.HasValue && OlumTarihi.Value.Date > DateTime.UtcNow.Date)
        {
            yield return new ValidationResult(
                "Ölüm tarihi gelecekte olamaz.",
                new[] { nameof(OlumTarihi) });
        }

        if (DogumTarihi.HasValue && DogumTarihi.Value.Date > DateTime.UtcNow.Date)
        {
            yield return new ValidationResult(
                "Doğum tarihi gelecekte olamaz.",
                new[] { nameof(DogumTarihi) });
        }

        if (AnneId.HasValue && BabaId.HasValue && AnneId.Value == BabaId.Value)
        {
            yield return new ValidationResult(
                "Anne ve baba aynı kişi olamaz.",
                new[] { nameof(AnneId), nameof(BabaId) });
        }
    }
}

public class PersonEditViewModel : PersonCreateViewModel
{
    public int Id { get; set; }

    public string? AnneAdSoyad { get; set; }

    public string? BabaAdSoyad { get; set; }

    public List<PersonPhotoViewModel> ExistingPhotos { get; set; } = new();
}

public class PersonPhotoViewModel
{
    public int Id { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsPrimary { get; set; }
}

public class PersonDetailViewModel
{
    public int Id { get; set; }
    public string Ad { get; set; } = string.Empty;
    public string Soyad { get; set; } = string.Empty;
    public string AdSoyad => $"{Ad} {Soyad}";
    public string? TcKimlikNoMasked { get; set; }
    public DateTime? DogumTarihi { get; set; }
    public DateTime? OlumTarihi { get; set; }
    public bool Hayatta => OlumTarihi == null;
    public string? Aciklama { get; set; }
    public string? DogumYeri { get; set; }
    public int? SulaleId { get; set; }
    public string? SulaleAdi { get; set; }

    public PersonListItemViewModel? Anne { get; set; }
    public PersonListItemViewModel? Baba { get; set; }

    public List<PersonListItemViewModel> Esler { get; set; } = new();
    public List<PersonListItemViewModel> Cocuklar { get; set; } = new();
    public List<PersonListItemViewModel> Kardesler { get; set; } = new();
    public List<PersonListItemViewModel> Torunlar { get; set; } = new();
    public List<PersonListItemViewModel> Yegenler { get; set; } = new();

    /// <summary>Anne ve baba tarafından büyükanne/büyükbabalar; her öğenin Rol alanı (Dede/Nine + taraf) doludur.</summary>
    public List<PersonListItemViewModel> BuyukebeveynLer { get; set; } = new();
    public List<PersonListItemViewModel> Amcalar { get; set; } = new();
    public List<PersonListItemViewModel> Dayilar { get; set; } = new();
    public List<PersonListItemViewModel> Halalar { get; set; } = new();
    public List<PersonListItemViewModel> Teyzeler { get; set; } = new();
    public List<PersonListItemViewModel> Kuzenler { get; set; } = new();

    public List<PersonPhotoViewModel> Photos { get; set; } = new();
}
