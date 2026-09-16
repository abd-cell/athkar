using Athkar.Shareds.Models.Base;

namespace Athkar.Areas.Domain.Notifications;

public class BroadcastTranslation : TranslationEntity
{
    public int BroadcastId { get; set; }
    public Broadcast? Broadcast { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}
