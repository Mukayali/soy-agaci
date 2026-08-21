using FamilyTree.Data;
using FamilyTree.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace FamilyTree.Services;

public class PhotoService : IPhotoService
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    private static readonly Dictionary<string, List<byte[]>> AllowedSignatures = new()
    {
        [".jpg"] = new List<byte[]> { new byte[] { 0xFF, 0xD8, 0xFF } },
        [".jpeg"] = new List<byte[]> { new byte[] { 0xFF, 0xD8, 0xFF } },
        [".png"] = new List<byte[]> { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } },
        [".webp"] = new List<byte[]> { new byte[] { 0x52, 0x49, 0x46, 0x46 } }, // RIFF header; WEBP marker checked separately
    };

    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public PhotoService(ApplicationDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    public async Task<PhotoUploadResult> SavePhotoAsync(int? personId, IFormFile file, bool isPrimary)
    {
        if (file.Length == 0)
        {
            return new PhotoUploadResult { Success = false, ErrorMessage = "Dosya boş olamaz." };
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return new PhotoUploadResult { Success = false, ErrorMessage = "Dosya boyutu 10 MB'ı aşamaz." };
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!AllowedSignatures.ContainsKey(extension))
        {
            return new PhotoUploadResult { Success = false, ErrorMessage = "Sadece JPG, JPEG, PNG veya WEBP formatları desteklenir." };
        }

        if (!await HasValidSignatureAsync(file, extension))
        {
            return new PhotoUploadResult { Success = false, ErrorMessage = "Dosya içeriği beklenen resim formatıyla eşleşmiyor." };
        }

        if (personId.HasValue)
        {
            var personExists = await _context.Persons.AnyAsync(p => p.Id == personId.Value);
            if (!personExists)
            {
                return new PhotoUploadResult { Success = false, ErrorMessage = "Kişi bulunamadı." };
            }
        }

        var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "persons");
        Directory.CreateDirectory(uploadsFolder);

        var uniqueFileName = $"{Guid.NewGuid()}{extension}";
        var fullPath = Path.Combine(uploadsFolder, uniqueFileName);

        await using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var makesPrimary = personId.HasValue &&
            (isPrimary || !await _context.PersonPhotos.AnyAsync(p => p.PersonId == personId));

        if (makesPrimary)
        {
            var existingPrimaries = await _context.PersonPhotos
                .Where(p => p.PersonId == personId && p.IsPrimary)
                .ToListAsync();

            foreach (var existing in existingPrimaries)
            {
                existing.IsPrimary = false;
            }
        }

        var photo = new PersonPhoto
        {
            PersonId = personId,
            FileName = file.FileName,
            FilePath = $"/uploads/persons/{uniqueFileName}",
            IsPrimary = makesPrimary,
            CreatedAt = DateTime.UtcNow,
        };

        _context.PersonPhotos.Add(photo);
        await _context.SaveChangesAsync();

        return new PhotoUploadResult { Success = true, Photo = photo };
    }

    public async Task<bool> AssignToPersonAsync(int photoId, int personId)
    {
        var photo = await _context.PersonPhotos.FindAsync(photoId);
        if (photo == null)
        {
            return false;
        }

        var personExists = await _context.Persons.AnyAsync(p => p.Id == personId);
        if (!personExists)
        {
            return false;
        }

        photo.PersonId = personId;

        var hasPrimary = await _context.PersonPhotos.AnyAsync(p => p.PersonId == personId && p.IsPrimary);
        photo.IsPrimary = !hasPrimary;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeletePhotoAsync(int photoId)
    {
        var photo = await _context.PersonPhotos.FindAsync(photoId);
        if (photo == null)
        {
            return false;
        }

        var physicalPath = Path.Combine(_environment.WebRootPath, photo.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(physicalPath))
        {
            File.Delete(physicalPath);
        }

        var wasPrimary = photo.IsPrimary;
        var personId = photo.PersonId;

        _context.PersonPhotos.Remove(photo);
        await _context.SaveChangesAsync();

        if (wasPrimary)
        {
            var next = await _context.PersonPhotos
                .Where(p => p.PersonId == personId)
                .OrderBy(p => p.CreatedAt)
                .FirstOrDefaultAsync();

            if (next != null)
            {
                next.IsPrimary = true;
                await _context.SaveChangesAsync();
            }
        }

        return true;
    }

    public async Task SetPrimaryAsync(int personId, int photoId)
    {
        var photos = await _context.PersonPhotos
            .Where(p => p.PersonId == personId)
            .ToListAsync();

        foreach (var photo in photos)
        {
            photo.IsPrimary = photo.Id == photoId;
        }

        await _context.SaveChangesAsync();
    }

    private static async Task<bool> HasValidSignatureAsync(IFormFile file, string extension)
    {
        var signatures = AllowedSignatures[extension];
        var maxSignatureLength = signatures.Max(s => s.Length);

        await using var stream = file.OpenReadStream();
        var buffer = new byte[Math.Max(maxSignatureLength, 12)];
        var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length));

        if (bytesRead < maxSignatureLength)
        {
            return false;
        }

        var headerMatches = signatures.Any(signature => buffer.Take(signature.Length).SequenceEqual(signature));

        if (!headerMatches)
        {
            return false;
        }

        if (extension == ".webp")
        {
            if (bytesRead < 12)
            {
                return false;
            }

            var webpMarker = System.Text.Encoding.ASCII.GetString(buffer, 8, 4);
            return webpMarker == "WEBP";
        }

        return true;
    }
}
