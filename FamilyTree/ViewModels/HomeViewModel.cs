namespace FamilyTree.ViewModels;

public class HomeViewModel
{
    public int ToplamKisi { get; set; }
    public int ToplamFotograf { get; set; }
    public List<PersonListItemViewModel> SonEklenenler { get; set; } = new();

    /// <summary>Doğum tarihinin ay/günü bugüne denk gelen kişiler (yıldan bağımsız).</summary>
    public List<BirthdayCardViewModel> BugunDoganlar { get; set; } = new();

    /// <summary>Ölüm tarihinin ay/günü bugüne denk gelen kişiler (yıldan bağımsız).</summary>
    public List<BirthdayCardViewModel> BugunKaybettiklerimiz { get; set; } = new();
}

public class BirthdayCardViewModel
{
    public int Id { get; set; }
    public string AdSoyad { get; set; } = string.Empty;
    public int? DogumYili { get; set; }

    /// <summary>Doğanlar kartında güncel yaş, Kaybettiklerimiz kartında vefat ettiğindeki yaş.</summary>
    public int? Yas { get; set; }
    public string? PrimaryPhotoPath { get; set; }
}
