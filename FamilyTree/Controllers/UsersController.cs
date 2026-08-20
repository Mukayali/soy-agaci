using FamilyTree.Models;
using FamilyTree.Services;
using FamilyTree.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FamilyTree.Controllers;

[Authorize(Roles = "Admin")]
public class UsersController : Controller
{
    private static readonly string[] AvailableRoles = { "Admin", "Editor", "Viewer" };

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditLogService _auditLogService;

    public UsersController(UserManager<ApplicationUser> userManager, IAuditLogService auditLogService)
    {
        _userManager = userManager;
        _auditLogService = auditLogService;
    }

    public async Task<IActionResult> Index()
    {
        var users = _userManager.Users.OrderBy(u => u.Email).ToList();
        var items = new List<UserListItemViewModel>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            items.Add(new UserListItemViewModel
            {
                Id = user.Id,
                Email = user.Email ?? user.UserName ?? string.Empty,
                Roller = roles.ToList(),
                KilitliMi = user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow,
                CreatedAt = user.CreatedAt,
            });
        }

        return View(items);
    }

    public IActionResult Create()
    {
        ViewBag.Roles = AvailableRoles;
        return View(new UserCreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserCreateViewModel model)
    {
        ViewBag.Roles = AvailableRoles;

        if (!AvailableRoles.Contains(model.Rol))
        {
            ModelState.AddModelError(nameof(model.Rol), "Geçersiz rol.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow,
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        await _userManager.AddToRoleAsync(user, model.Rol);
        await _auditLogService.LogAsync($"Kullanıcı oluşturuldu (rol: {model.Rol})", "User", null);

        TempData["SuccessMessage"] = "Kullanıcı başarıyla oluşturuldu.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> EditRole(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var roles = await _userManager.GetRolesAsync(user);
        ViewBag.Roles = AvailableRoles;

        return View(new UserEditRoleViewModel
        {
            Id = user.Id,
            Email = user.Email ?? user.UserName ?? string.Empty,
            Rol = roles.FirstOrDefault() ?? "Viewer",
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditRole(string id, UserEditRoleViewModel model)
    {
        ViewBag.Roles = AvailableRoles;

        if (id != model.Id || !AvailableRoles.Contains(model.Rol))
        {
            return BadRequest();
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var currentRoles = await _userManager.GetRolesAsync(user);

        if (currentRoles.Contains("Admin") && model.Rol != "Admin" && await IsLastAdminAsync(user))
        {
            ModelState.AddModelError(string.Empty, "Sistemdeki son Admin kullanıcısının rolü değiştirilemez.");
            model.Email = user.Email ?? user.UserName ?? string.Empty;
            return View(model);
        }

        await _userManager.RemoveFromRolesAsync(user, currentRoles);
        await _userManager.AddToRoleAsync(user, model.Rol);
        await _auditLogService.LogAsync($"Kullanıcı rolü değiştirildi: {model.Rol}", "User", null);

        TempData["SuccessMessage"] = "Kullanıcı rolü güncellendi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        if (user.Id == _userManager.GetUserId(User))
        {
            TempData["ErrorMessage"] = "Kendi hesabınızı silemezsiniz.";
            return RedirectToAction(nameof(Index));
        }

        if (await IsLastAdminAsync(user))
        {
            TempData["ErrorMessage"] = "Sistemdeki son Admin kullanıcısı silinemez.";
            return RedirectToAction(nameof(Index));
        }

        await _userManager.DeleteAsync(user);
        await _auditLogService.LogAsync("Kullanıcı silindi", "User", null);

        TempData["SuccessMessage"] = "Kullanıcı silindi.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<bool> IsLastAdminAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        if (!roles.Contains("Admin"))
        {
            return false;
        }

        var admins = await _userManager.GetUsersInRoleAsync("Admin");
        return admins.Count <= 1;
    }
}
