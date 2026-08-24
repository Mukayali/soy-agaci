using System.ComponentModel.DataAnnotations;

namespace FamilyTree.Models;

/// <summary>
/// Bir aile grubunu/sülaleyi temsil eder. Kişiler opsiyonel olarak bir veya birden fazla
/// sülaleye etiketlenebilir (bkz. PersonSulale); bir sülale silindiğinde kişiler silinmez,
/// yalnızca o sülaleyle ilişkisi kalkar.
/// </summary>
public class Sulale
{
    public int Id { get; set; }

    [Required]
    [MaxLength(150)]
    public string Ad { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Aciklama { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PersonSulale> PersonSulaleler { get; set; } = new List<PersonSulale>();
}
