using Athkar.Areas.Services.Support.Models;
using Athkar.Shareds.Attributes;
using Athkar.Shareds.Enums;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Services.Support;

[ScopedInjectable]
public interface ISupportService
{
    /// <summary>The published help topics in one language. Anonymous.</summary>
    Task<BaseResponse<List<FaqOutput>>> Faq(string? languageCode);

    /// <summary>A reader writes in. Device-scoped and rate-limited by open count.</summary>
    Task<BaseResponse> SubmitFeedback(FeedbackInput input);

    // ── Desk side ──
    Task<BaseResponse<PageOutput<AdminFaqOutput>>> ListFaq(PageInput input);
    Task<BaseResponse<AdminFaqOutput>> CreateFaq(FaqInput input);
    Task<BaseResponse<AdminFaqOutput>> UpdateFaq(int id, FaqInput input);
    Task<BaseResponse> DeleteFaq(int id);

    Task<BaseResponse<PageOutput<FeedbackOutput>>> ListFeedback(FeedbackStatus? status, PageInput input);

    /// <summary>
    /// Answers a reader. The reply is delivered as a notification to the device
    /// that wrote in — there is no account to email, and that limitation is
    /// stated in the app rather than worked around.
    /// </summary>
    Task<BaseResponse<FeedbackOutput>> Reply(int id, FeedbackReplyInput input);
}
