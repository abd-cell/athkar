using Athkar.Shareds.Models.Base;

namespace Athkar.Areas.Domain.Reminders;

public class ReminderCampaignTranslation : TranslationEntity
{
    public int CampaignId { get; set; }
    public ReminderCampaign? Campaign { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}
