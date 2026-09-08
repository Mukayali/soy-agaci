using FamilyTree.Data;
using FamilyTree.Models;
using FamilyTree.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace FamilyTree.Services;

public class PersonCommentService : IPersonCommentService
{
    private const int MaxYorumLength = 2000;

    private readonly ApplicationDbContext _context;

    public PersonCommentService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<PersonCommentViewModel>> GetByPersonIdAsync(int personId)
    {
        return await _context.PersonComments
            .AsNoTracking()
            .Where(c => c.PersonId == personId)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new PersonCommentViewModel
            {
                Id = c.Id,
                KullaniciAdi = c.KullaniciAdi,
                Yorum = c.Yorum,
                CreatedAt = c.CreatedAt,
            })
            .ToListAsync();
    }

    public async Task<AdminCommentListViewModel> GetRecentAsync(int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _context.PersonComments
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedAt);

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new AdminCommentItemViewModel
            {
                Id = c.Id,
                PersonId = c.PersonId,
                PersonAdSoyad = c.Person.Ad + " " + c.Person.Soyad,
                KullaniciAdi = c.KullaniciAdi,
                Yorum = c.Yorum,
                CreatedAt = c.CreatedAt,
            })
            .ToListAsync();

        return new AdminCommentListViewModel
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<(bool Success, string? ErrorMessage)> AddCommentAsync(int personId, string? userId, string kullaniciAdi, string yorum)
    {
        var trimmed = yorum?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            return (false, "Yorum boş olamaz.");
        }

        if (trimmed.Length > MaxYorumLength)
        {
            return (false, $"Yorum en fazla {MaxYorumLength} karakter olabilir.");
        }

        var personExists = await _context.Persons.AnyAsync(p => p.Id == personId);
        if (!personExists)
        {
            return (false, "Kişi bulunamadı.");
        }

        _context.PersonComments.Add(new PersonComment
        {
            PersonId = personId,
            UserId = userId,
            KullaniciAdi = string.IsNullOrWhiteSpace(kullaniciAdi) ? "Bilinmeyen Kullanıcı" : kullaniciAdi,
            Yorum = trimmed,
            CreatedAt = DateTime.UtcNow,
        });

        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<bool> DeleteCommentAsync(int commentId)
    {
        var comment = await _context.PersonComments.FindAsync(commentId);
        if (comment == null)
        {
            return false;
        }

        _context.PersonComments.Remove(comment);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<int> DeleteCommentsAsync(IEnumerable<int> commentIds)
    {
        var ids = commentIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return 0;
        }

        var comments = await _context.PersonComments
            .Where(c => ids.Contains(c.Id))
            .ToListAsync();

        if (comments.Count == 0)
        {
            return 0;
        }

        _context.PersonComments.RemoveRange(comments);
        await _context.SaveChangesAsync();
        return comments.Count;
    }
}
