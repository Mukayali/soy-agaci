using System.ComponentModel.DataAnnotations;

namespace FamilyTree.Models;

public class PersonPhoto
{
    public int Id { get; set; }

    /// <summary>
    /// Null olabilir: bir kişiye henüz atanmamış ("ilişkilendirilmemiş") fotoğrafları
    /// (fotoğraf galerisinden kişi seçmeden yüklenenler) temsil eder.
    /// </summary>
    public int? PersonId { get; set; }

    [Required]
    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(512)]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsPrimary { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Person? Person { get; set; }
}
