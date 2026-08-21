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
}
