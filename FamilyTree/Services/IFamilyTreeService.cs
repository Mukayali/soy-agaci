using FamilyTree.ViewModels;

namespace FamilyTree.Services;

public interface IFamilyTreeService
{
    /// <summary>
    /// Seçilen kişiyi merkez alan temel ağacı döndürür: anne, baba, eş(ler), kardeşler, çocuklar.
    /// </summary>
    Task<FamilyTreeGraphDto?> GetBaseTreeAsync(int personId);

    /// <summary>Anne ve babanın anne/babalarını (büyükebeveynleri) döndürür.</summary>
    Task<FamilyTreeGraphDto> GetGrandparentsAsync(int personId);

    /// <summary>Çocukların çocuklarını (torunları) döndürür.</summary>
    Task<FamilyTreeGraphDto> GetGrandchildrenAsync(int personId);

    /// <summary>Kardeşlerin çocuklarını (yeğenleri) döndürür.</summary>
    Task<FamilyTreeGraphDto> GetNephewsAsync(int personId);
}
