using FamilyTree.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FamilyTree.Controllers;

[Authorize(Roles = "Admin")]
public class BackupController : Controller
{
    private readonly IBackupService _backupService;
    private readonly IAuditLogService _auditLogService;

    public BackupController(IBackupService backupService, IAuditLogService auditLogService)
    {
        _backupService = backupService;
        _auditLogService = auditLogService;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create()
    {
        var result = await _backupService.CreateBackupAsync();

        if (!result.Success || result.Data == null)
        {
            TempData["ErrorMessage"] = result.ErrorMessage ?? "Yedekleme başarısız oldu.";
            return RedirectToAction(nameof(Index));
        }

        await _auditLogService.LogAsync("Veritabanı yedeği alındı", "Database");

        return File(result.Data, "application/sql", result.FileName);
    }
}
