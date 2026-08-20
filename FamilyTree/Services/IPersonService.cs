using FamilyTree.ViewModels;

namespace FamilyTree.Services;

public interface IPersonService
{
    Task<PersonDetailViewModel?> GetDetailsAsync(int id);

    Task<PersonEditViewModel?> GetForEditAsync(int id);

    Task<PersonIndexViewModel> SearchAsync(string? query, int page = 1, int pageSize = 20);

    Task<List<PersonSearchResultItem>> QuickSearchAsync(string query, int? excludePersonId = null, int limit = 10);

    Task<(bool Success, int? Id, string? ErrorMessage)> CreateAsync(PersonCreateViewModel model);

    Task<(bool Success, string? ErrorMessage)> UpdateAsync(PersonEditViewModel model);

    Task<(bool Success, string? ErrorMessage, int ChildCount, int SpouseCount)> GetDeleteInfoAsync(int id);

    Task<bool> DeleteAsync(int id);
}
