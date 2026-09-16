namespace Athkar.Shareds.Constants;

/// <summary>
/// The action names written to the audit trail.
///
/// Constants rather than an enum: the column is a string so a new action never
/// needs a migration, and the dotted names sort into feature groups when a
/// reviewer filters the log.
/// </summary>
public static class AuditActions
{
    public const string CategoryCreate = "content.category.create";
    public const string CategoryUpdate = "content.category.update";
    public const string CategoryDelete = "content.category.delete";
    public const string CategoryReorder = "content.category.reorder";

    public const string DhikrCreate = "content.dhikr.create";
    public const string DhikrUpdate = "content.dhikr.update";
    public const string DhikrDelete = "content.dhikr.delete";
    public const string DhikrPublish = "content.dhikr.publish";
    public const string DhikrUnpublish = "content.dhikr.unpublish";

    public const string LanguageCreate = "localization.language.create";
    public const string LanguageUpdate = "localization.language.update";
    public const string LanguageDelete = "localization.language.delete";
    public const string UiStringsImport = "localization.strings.import";

    public const string ReminderCreate = "reminders.campaign.create";
    public const string ReminderUpdate = "reminders.campaign.update";
    public const string ReminderDelete = "reminders.campaign.delete";

    public const string BroadcastCreate = "notifications.broadcast.create";
    public const string BroadcastUpdate = "notifications.broadcast.update";
    public const string BroadcastSend = "notifications.broadcast.send";
    public const string BroadcastCancel = "notifications.broadcast.cancel";

    public const string DispatchRetry = "notifications.dispatch.retry";
    public const string DispatchCancel = "notifications.dispatch.cancel";

    /// <summary>An admin ran the pipeline by hand instead of waiting for the workers.</summary>
    public const string PushRunNow = "notifications.push.run";

    public const string QuranPackageUpload = "quran.package.upload";
    public const string QuranPackagePublish = "quran.package.publish";

    /// <summary>A published mushaf was withdrawn, leaving nothing to download.</summary>
    public const string QuranPackageUnpublish = "quran.package.unpublish";

    /// <summary>
    /// Which mushaf a device gets when it names none was changed. Worth a line
    /// of its own: it silently redirects every fresh install, and nothing else
    /// in the console has that reach.
    /// </summary>
    public const string QuranPackageSetDefault = "quran.package.setDefault";

    /// <summary>What a package says about itself was corrected — never its bytes.</summary>
    public const string QuranPackageUpdate = "quran.package.update";
    public const string QuranPackageDelete = "quran.package.delete";

    /// <summary>
    /// An admin re-read the Qur'anic adhkar from the canonical source. Logged
    /// with the before and after of every row it touched — this is the one
    /// action in the system that rewrites narrated text without a human typing
    /// the replacement, so the trail has to show exactly what changed.
    /// </summary>
    public const string QuranAthkarSync = "quran.athkar.sync";

    /// <summary>
    /// An admin imported the أبواب and adhkar of حصن المسلم. Logged per chapter
    /// with what it added, because this is the one action that can put hundreds
    /// of rows into the content tables in a single click.
    /// </summary>
    public const string AdhkarImport = "content.adhkar.import";

    /// <summary>
    /// An admin filled the imported drafts' attribution from حصن المسلم's own
    /// footnotes. Logged per row with the before and after, because it writes
    /// the one field publishing is gated on — and because an attribution nobody
    /// can trace back to where it came from is the exact thing this project
    /// exists to not have.
    /// </summary>
    public const string AdhkarTakhrijSync = "content.adhkar.takhrij";

    public const string ConfigurationUpdate = "admin.configuration.update";
    public const string WidgetUpdate = "admin.widget.update";
    public const string WidgetCatalogCreate = "admin.widget.catalog.create";
    public const string WidgetCatalogUpdate = "admin.widget.catalog.update";
    public const string WidgetCatalogDelete = "admin.widget.catalog.delete";
    public const string WidgetCatalogReorder = "admin.widget.catalog.reorder";

    public const string StaffCreate = "admin.staff.create";
    public const string StaffUpdate = "admin.staff.update";
    public const string StaffDelete = "admin.staff.delete";
    public const string StaffPasswordReset = "admin.staff.password-reset";

    /// <summary>A staff session was ended by somebody other than its owner signing out.</summary>
    public const string SessionRevoke = "admin.session.revoke";

    /// <summary>
    /// An admin read one install's push token. Logged because the token is a
    /// capability — whoever holds it, with the server key, can raise a
    /// notification on that phone — so reading one is an event, not a view.
    /// </summary>
    public const string DevicePushTokenReveal = "admin.device.push-token.reveal";

    public const string FaqCreate = "support.faq.create";
    public const string FaqUpdate = "support.faq.update";
    public const string FaqDelete = "support.faq.delete";
    public const string FeedbackReply = "support.feedback.reply";
    public const string FeedbackStatusChange = "support.feedback.status";
}
