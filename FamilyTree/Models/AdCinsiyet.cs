using System.ComponentModel.DataAnnotations;

namespace FamilyTree.Models;

/// <summary>
/// Ada göre cinsiyet eşlemesi için referans listesi. Kullanıcının yüklediği Excel/CSV
/// dosyasından doldurulur ve <c>Person.Cinsiyet</c> alanını isimlere göre otomatik
/// doldurmak için kullanılır. <see cref="Ad"/> daima Türkçe kültürüyle BÜYÜK harfe
/// normalize edilmiş biçimde saklanır (eşleştirme bunun üzerinden yapılır).
/// </summary>
public class AdCinsiyet
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Ad { get; set; } = string.Empty;

    public Gender Cinsiyet { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
