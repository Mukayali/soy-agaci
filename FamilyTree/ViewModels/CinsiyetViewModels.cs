using FamilyTree.Models;

namespace FamilyTree.ViewModels;

public class CinsiyetYonetimViewModel
{
    public int ReferansSayisi { get; set; }

    /// <summary>Referans listesinden ilk birkaç kayıt (önizleme amaçlı).</summary>
    public List<AdCinsiyet> ReferansOrnek { get; set; } = new();

    public bool EksikleriGoster { get; set; }

    public List<EksikCinsiyetKisiViewModel> EksikKisiler { get; set; } = new();
    public int EksikToplam { get; set; }
    public int SayfaNo { get; set; } = 1;
    public int SayfaBoyutu { get; set; } = 100;
    public int ToplamSayfa => EksikToplam == 0 ? 1 : (int)Math.Ceiling(EksikToplam / (double)SayfaBoyutu);
}

public class EksikCinsiyetKisiViewModel
{
    public int Id { get; set; }
    public string Ad { get; set; } = string.Empty;
    public string Soyad { get; set; } = string.Empty;
    public string AdSoyad => $"{Ad} {Soyad}";
    public int? DogumYili { get; set; }
    public int? OlumYili { get; set; }
    public string? AnneAdSoyad { get; set; }
    public string? BabaAdSoyad { get; set; }
}
