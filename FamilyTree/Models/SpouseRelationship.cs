using System.ComponentModel.DataAnnotations.Schema;

namespace FamilyTree.Models;

public class SpouseRelationship
{
    public int Id { get; set; }

    public int Person1Id { get; set; }

    public int Person2Id { get; set; }

    [Column(TypeName = "date")]
    public DateTime? MarriageDate { get; set; }

    [Column(TypeName = "date")]
    public DateTime? DivorceDate { get; set; }

    public Person Person1 { get; set; } = null!;

    public Person Person2 { get; set; } = null!;

    [NotMapped]
    public bool Aktif => DivorceDate == null;
}
