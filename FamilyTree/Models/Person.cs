using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FamilyTree.Models;

public class Person
{
    public int Id { get; set; }

    [MaxLength(11)]
    public string? TcKimlikNo { get; set; }

    [Required]
    [MaxLength(100)]
    public string Ad { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Soyad { get; set; } = string.Empty;

    [Column(TypeName = "date")]
    public DateTime? DogumTarihi { get; set; }

    [Column(TypeName = "date")]
    public DateTime? OlumTarihi { get; set; }

    public string? Aciklama { get; set; }

    [MaxLength(200)]
    public string? DogumYeri { get; set; }

    public Gender? Cinsiyet { get; set; }

    public ICollection<PersonSulale> PersonSulaleler { get; set; } = new List<PersonSulale>();

    public int? AnneId { get; set; }

    public int? BabaId { get; set; }

    public Person? Anne { get; set; }

    public Person? Baba { get; set; }

    public ICollection<Person> AnneCocuklari { get; set; } = new List<Person>();

    public ICollection<Person> BabaCocuklari { get; set; } = new List<Person>();

    public ICollection<PersonPhoto> Photos { get; set; } = new List<PersonPhoto>();

    public ICollection<SpouseRelationship> SpouseRelationshipsAsPerson1 { get; set; } = new List<SpouseRelationship>();

    public ICollection<SpouseRelationship> SpouseRelationshipsAsPerson2 { get; set; } = new List<SpouseRelationship>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    [NotMapped]
    public string AdSoyad => $"{Ad} {Soyad}";

    [NotMapped]
    public bool Hayatta => OlumTarihi == null;
}
