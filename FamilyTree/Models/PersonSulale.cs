namespace FamilyTree.Models;

/// <summary>
/// Person ile Sulale arasındaki çoktan-çoğa (many-to-many) ilişkiyi temsil eder — bir kişi
/// birden fazla sülalenin üyesi olabilir (ör. hem baba tarafı hem anne tarafı sülalesi).
/// </summary>
public class PersonSulale
{
    public int Id { get; set; }

    public int PersonId { get; set; }

    public int SulaleId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Person Person { get; set; } = null!;

    public Sulale Sulale { get; set; } = null!;
}
