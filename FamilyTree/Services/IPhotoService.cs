using FamilyTree.Models;
using Microsoft.AspNetCore.Http;

namespace FamilyTree.Services;

public class PhotoUploadResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public PersonPhoto? Photo { get; set; }
}

public interface IPhotoService
{
    /// <summary>personId null verilirse fotoğraf "ilişkilendirilmemiş" olarak kaydedilir.</summary>
    Task<PhotoUploadResult> SavePhotoAsync(int? personId, IFormFile file, bool isPrimary);

    Task<bool> DeletePhotoAsync(int photoId);

    Task SetPrimaryAsync(int personId, int photoId);

    /// <summary>İlişkilendirilmemiş bir fotoğrafı bir kişiye atar.</summary>
    Task<bool> AssignToPersonAsync(int photoId, int personId);

    /// <summary>
    /// İlişkilendirilmemiş birden fazla fotoğrafı bir kişiye atar. Yalnızca hiçbir kişiye
    /// atanmamış (PersonId == null) fotoğraflar taşınır; atanan fotoğraf sayısını döndürür.
    /// </summary>
    Task<int> AssignManyToPersonAsync(IEnumerable<int> photoIds, int personId);

    /// <summary>
    /// Yanlış kişiyle eşleştirilmiş bir fotoğrafı başka bir kişiye taşır. Eski kişinin ana
    /// fotoğrafıysa yerine kalan bir fotoğrafını ana yapar; yeni kişinin ana fotoğrafı yoksa
    /// bunu ana yapar.
    /// </summary>
    Task<(bool Success, string? ErrorMessage, int? OldPersonId)> ReassignPhotoAsync(int photoId, int newPersonId);

    /// <summary>Var olan bir fotoğrafın dosyasını (ör. kırpma/döndürme sonrası) yenisiyle değiştirir; kayıt/rol/açıklama gibi diğer alanlar korunur.</summary>
    Task<PhotoUploadResult> ReplacePhotoFileAsync(int photoId, IFormFile file);
}
