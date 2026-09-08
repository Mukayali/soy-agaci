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

    /// <summary>Anne ve babanın kardeşlerini döndürür (cinsiyete göre amca/dayı/hala/teyze olarak etiketlenir).</summary>
    Task<FamilyTreeGraphDto> GetAuntsUnclesAsync(int personId);

    /// <summary>Amca/dayı/hala/teyzelerin çocuklarını (kuzenleri) döndürür.</summary>
    Task<FamilyTreeGraphDto> GetCousinsAsync(int personId);

    /// <summary>
    /// Bir sülaledeki tüm kişileri, aralarındaki anne/baba/eş ilişkileriyle birlikte döndürür.
    /// Sülale bulunamazsa null döner; üyesi yoksa boş bir graph döner.
    /// </summary>
    Task<FamilyTreeGraphDto?> GetBySulaleAsync(int sulaleId);

    /// <summary>
    /// İki kişi arasında kayıtlı anne/baba ve eş ilişkileri üzerinden en kısa bağı bulur.
    /// Bağ yoksa <see cref="RelationshipResultDto.Related"/> false döner.
    /// </summary>
    Task<RelationshipResultDto> FindRelationshipAsync(int person1Id, int person2Id);
}
