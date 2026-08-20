namespace FamilyTree.ViewModels;

public class HomeViewModel
{
    public int ToplamKisi { get; set; }
    public int ToplamFotograf { get; set; }
    public List<PersonListItemViewModel> SonEklenenler { get; set; } = new();
}
