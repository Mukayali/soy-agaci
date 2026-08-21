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
