namespace FamilyTree.ViewModels;

public class FamilyTreeNodeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public string Soyad { get; set; } = string.Empty;
    public int? BirthYear { get; set; }
    public int? DeathYear { get; set; }
    public bool Alive { get; set; }
    public string? PhotoPath { get; set; }
    public int Generation { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool IsCenter { get; set; }
}

public class FamilyTreeLinkDto
{
    public int Source { get; set; }
    public int Target { get; set; }

    // "parent" (source is a parent of target) or "spouse" (source/target are spouses)
    public string Relationship { get; set; } = string.Empty;
}

public class FamilyTreeGraphDto
{
    public List<FamilyTreeNodeDto> Nodes { get; set; } = new();
    public List<FamilyTreeLinkDto> Links { get; set; } = new();
}

/// <summary>İki kişi arasındaki en kısa akrabalık yolunun tek bir adımı (X, Y'nin &lt;ilişki&gt;'si).</summary>
public class RelationshipStepDto
{
    public int FromId { get; set; }
    public string FromName { get; set; } = string.Empty;
    public int ToId { get; set; }
    public string ToName { get; set; } = string.Empty;

    /// <summary>3. tekil iyelik ekli ilişki adı: "annesi", "babası", "çocuğu", "eşi".</summary>
    public string Relation { get; set; } = string.Empty;
}

/// <summary>İki kişi arasındaki tek bir olası akrabalık yolu.</summary>
public class RelationshipPathDto
{
    /// <summary>Bu yola özgü okunabilir başlık, ör. "1. dereceden kuzenler".</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>"Kan bağı", "Evlilik bağı" veya "Kan + evlilik".</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Adım sayısı (kişi sayısı - 1).</summary>
    public int Length { get; set; }

    /// <summary>Yol boyunca her adımın açıklaması.</summary>
    public List<RelationshipStepDto> Steps { get; set; } = new();

    /// <summary>1. kişiden 2. kişiye giden yoldaki kişi Id'leri, sırayla. Harita üzerinde vurgulanır.</summary>
    public List<int> PersonIds { get; set; } = new();
}

/// <summary>
/// İki kişi arasında kayıtlı anne/baba ve eş ilişkileri üzerinden bulunan bağ(lar)ın sonucu.
/// Birden fazla farklı yol bulunabilir (ör. hem kan bağı hem evlilik yoluyla).
/// </summary>
public class RelationshipResultDto
{
    public bool Related { get; set; }

    public int Person1Id { get; set; }
    public string Person1Name { get; set; } = string.Empty;
    public int Person2Id { get; set; }
    public string Person2Name { get; set; } = string.Empty;

    /// <summary>Birincil (en kısa) yolun başlığı; ilişki yoksa bilgi mesajı.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Bulunan tüm yollar, en kısası ilk sırada. En az bir öğe varsa <see cref="Related"/> true'dur.</summary>
    public List<RelationshipPathDto> Paths { get; set; } = new();

    /// <summary>Tüm yolların birleşimindeki kişileri ve bağları içeren, haritada çizilecek grafik.</summary>
    public FamilyTreeGraphDto Graph { get; set; } = new();
}
