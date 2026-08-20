using System.ComponentModel.DataAnnotations;

namespace FamilyTree.Models;

public enum Gender
{
    [Display(Name = "Erkek")]
    Erkek,

    [Display(Name = "Kadın")]
    Kadin,
}
