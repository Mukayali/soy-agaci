using Microsoft.AspNetCore.Identity;

namespace FamilyTree.Models;

public class ApplicationUser : IdentityUser
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
