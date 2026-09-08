using FamilyTree.ViewModels;

namespace FamilyTree.Services;

public interface IPersonCommentService
{
    Task<List<PersonCommentViewModel>> GetByPersonIdAsync(int personId);

    Task<AdminCommentListViewModel> GetRecentAsync(int page, int pageSize);

    Task<(bool Success, string? ErrorMessage)> AddCommentAsync(int personId, string? userId, string kullaniciAdi, string yorum);

    Task<bool> DeleteCommentAsync(int commentId);

    Task<int> DeleteCommentsAsync(IEnumerable<int> commentIds);
}
