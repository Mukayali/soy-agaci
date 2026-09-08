using FamilyTree.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FamilyTree.Controllers;

[Authorize(Roles = "Admin")]
public class CommentsController : Controller
{
    private const int PageSize = 50;

    private readonly IPersonCommentService _commentService;
    private readonly IAuditLogService _auditLogService;

    public CommentsController(IPersonCommentService commentService, IAuditLogService auditLogService)
    {
        _commentService = commentService;
        _auditLogService = auditLogService;
    }

    public async Task<IActionResult> Index(int page = 1)
    {
        var vm = await _commentService.GetRecentAsync(page, PageSize);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int commentId, int? personId, int page = 1)
    {
        var deleted = await _commentService.DeleteCommentAsync(commentId);
        if (deleted)
        {
            await _auditLogService.LogAsync("Yorum kaldırıldı", "Person", personId);
            TempData["SuccessMessage"] = "Yorum kaldırıldı.";
        }
        else
        {
            TempData["ErrorMessage"] = "Yorum bulunamadı.";
        }

        return RedirectToAction(nameof(Index), new { page });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSelected(int[]? commentIds, int page = 1)
    {
        var count = await _commentService.DeleteCommentsAsync(commentIds ?? Array.Empty<int>());
        if (count > 0)
        {
            await _auditLogService.LogAsync($"{count} yorum toplu olarak kaldırıldı", "Person", null);
            TempData["SuccessMessage"] = $"{count} yorum kaldırıldı.";
        }
        else
        {
            TempData["ErrorMessage"] = "Kaldırılacak yorum seçilmedi.";
        }

        return RedirectToAction(nameof(Index), new { page });
    }
}
