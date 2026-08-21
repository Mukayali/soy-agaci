namespace FamilyTree.ViewModels;

public class PhotoGalleryViewModel
{
    public List<PersonPhotoGroupViewModel> Groups { get; set; } = new();
    public List<PersonPhotoViewModel> UnassignedPhotos { get; set; } = new();
    public int TotalPhotoCount { get; set; }
}

public class PersonPhotoGroupViewModel
{
    public int PersonId { get; set; }
    public string PersonName { get; set; } = string.Empty;
    public bool PersonIsDeleted { get; set; }
    public List<PersonPhotoViewModel> Photos { get; set; } = new();
}
