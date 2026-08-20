using System.ComponentModel.DataAnnotations;

namespace FamilyTree.ViewModels;

public class UserListItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public List<string> Roller { get; set; } = new();
    public bool KilitliMi { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UserCreateViewModel
{
    [Required(ErrorMessage = "E-posta alanı zorunludur.")]
    [EmailAddress]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Şifre alanı zorunludur.")]
    [MinLength(8, ErrorMessage = "Şifre en az 8 karakter olmalıdır.")]
    [DataType(DataType.Password)]
    [Display(Name = "Şifre")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Rol seçimi zorunludur.")]
    [Display(Name = "Rol")]
    public string Rol { get; set; } = "Viewer";
}

public class UserEditRoleViewModel
{
    public string Id { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Rol seçimi zorunludur.")]
    [Display(Name = "Rol")]
    public string Rol { get; set; } = string.Empty;
}
