using Athkar.Areas.Services.Content.Models;
using Athkar.Shareds.Attributes;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Services.Content;

/// <summary>
/// The CMS half of the content slice. Every method here bumps the content
/// version on success, which is how a published edit reaches a phone.
/// </summary>
[ScopedInjectable]
public interface IContentAdminService
{
    Task<BaseResponse<PageOutput<AdminCategoryOutput>>> ListCategories(PageInput input);
    Task<BaseResponse<AdminCategoryOutput>> GetCategory(int id);
    Task<BaseResponse<AdminCategoryOutput>> CreateCategory(CategoryInput input);
    Task<BaseResponse<AdminCategoryOutput>> UpdateCategory(int id, CategoryInput input);
    Task<BaseResponse> DeleteCategory(int id);
    Task<BaseResponse> ReorderCategories(ReorderInput input);

    Task<BaseResponse<PageOutput<AdminDhikrOutput>>> ListAdhkar(int? categoryId, PageInput input);
    Task<BaseResponse<AdminDhikrOutput>> GetDhikr(int id);
    Task<BaseResponse<AdminDhikrOutput>> CreateDhikr(DhikrInput input);
    Task<BaseResponse<AdminDhikrOutput>> UpdateDhikr(int id, DhikrInput input);
    Task<BaseResponse> DeleteDhikr(int id);
    Task<BaseResponse> ReorderAdhkar(ReorderInput input);

    /// <summary>
    /// Publishes or withdraws one dhikr. Separate from
    /// <see cref="UpdateDhikr"/> because it is the one content action a reviewer
    /// takes without editing anything, and it wants its own audit entry.
    /// </summary>
    Task<BaseResponse<AdminDhikrOutput>> SetDhikrPublished(int id, bool isPublished);
}
