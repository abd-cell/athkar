namespace Athkar.Shareds.Models;

/// <summary>
/// Typed error codes. Clients switch on the number and render their own copy —
/// the server never sends a sentence meant for a reader. See
/// <c>app/athkar_app/lib/core/error_messages.dart</c> and the CMS i18n files.
/// </summary>
public enum ErrorCode
{
    Success = 0,
    None = 0,

    // ── Generic ──
    UnknownError = 1,
    ValidationError = 2,
    NotFound = 3,
    Unauthorized = 4,
    Forbidden = 5,
    Conflict = 6,

    /// <summary>The upload is bigger than the endpoint will take.</summary>
    FileTooLarge = 7,

    /// <summary>Not a file type the endpoint accepts, or its bytes contradict its declared type.</summary>
    UnsupportedFileType = 8,

    // ── Staff accounts ──
    InvalidCredentials = 100,
    SessionExpired = 101,
    AccountDisabled = 102,

    /// <summary>The email is already on a staff account.</summary>
    EmailAlreadyRegistered = 103,

    /// <summary>The last administrator cannot be disabled, demoted or deleted.</summary>
    LastAdministrator = 104,

    /// <summary>No session row with that id — it has already been ended, or never existed.</summary>
    SessionNotFound = 105,

    /// <summary>
    /// Refused: the session belongs to an account above the caller on the
    /// ladder. An admin does not get to lock out the superadmin who could undo
    /// it. Revoking your own, or an equal's, is allowed.
    /// </summary>
    CannotRevokeHigherRole = 106,

    // ── Devices ──
    DeviceNotFound = 200,

    /// <summary>The device key is not a form the server will accept as an identity.</summary>
    InvalidDeviceKey = 201,

    // ── Content: categories and adhkar ──
    CategoryNotFound = 300,
    DhikrNotFound = 301,

    /// <summary>Another category or dhikr already uses that key.</summary>
    DuplicateKey = 302,

    /// <summary>
    /// A dhikr must carry its Arabic text — it is the primary source, not a
    /// translation of one, and every other language hangs off it.
    /// </summary>
    ArabicTextRequired = 303,

    /// <summary>
    /// Publishing was refused because the content has no attributed source.
    /// The project promises a takhrij beside every dhikr; an unsourced row may
    /// exist as a draft but must never reach a reader.
    /// </summary>
    SourceRequired = 304,

    /// <summary>The category still holds adhkar, so it cannot be removed.</summary>
    CategoryNotEmpty = 305,

    /// <summary>
    /// The shipped adhkar corpus is missing or unreadable, so there is nothing
    /// to import. A deployment problem rather than an editor's mistake — re-run
    /// <c>tools/adhkar/pull.py</c> and ship the file beside the seeders.
    /// </summary>
    AdhkarCorpusMissing = 306,

    /// <summary>
    /// The takhrij file is not beside the seeders, so there are no footnotes to
    /// attribute the drafts from. A deployment problem rather than an editor's
    /// mistake — re-run <c>tools/adhkar/pull_takhrij.py</c> and ship the file.
    /// </summary>
    TakhrijCatalogMissing = 307,

    /// <summary>No such radio station, or it has been removed.</summary>
    RadioStationNotFound = 308,

    /// <summary>
    /// The stream is not an https:// URL. Cleartext audio is blocked by iOS's
    /// ATS and by Android's default network policy, so a station saved with one
    /// would be silent on every phone rather than on some.
    /// </summary>
    InsecureStreamUrl = 309,

    // ── Languages and UI strings ──
    LanguageNotFound = 400,

    /// <summary>The code is already enabled.</summary>
    LanguageAlreadyExists = 401,

    /// <summary>The default language cannot be disabled or deleted — every fallback ends here.</summary>
    DefaultLanguageImmutable = 402,

    /// <summary>Arabic cannot be removed: it is the language the content is written in.</summary>
    SourceLanguageImmutable = 403,

    // ── Reminders and push ──
    ReminderNotFound = 500,

    /// <summary>The recurrence selects no days, so the campaign would never fire.</summary>
    ReminderHasNoDays = 501,

    /// <summary>A fixed-time campaign needs a local time of day; an anchored one needs an anchor.</summary>
    ReminderScheduleIncomplete = 502,

    /// <summary>
    /// A channel id the app has never created. Android fixes a channel's sound
    /// and importance the moment it exists, so the ids are a contract with every
    /// installed copy — an invented one would be dropped or silently rehomed on
    /// the reader's phone, where nobody could see it had happened.
    /// </summary>
    UnknownNotificationChannel = 506,

    BroadcastNotFound = 503,

    /// <summary>The broadcast has already gone out; a sent message cannot be edited or re-scheduled.</summary>
    BroadcastAlreadySent = 504,

    /// <summary>The scheduled moment is in the past.</summary>
    ScheduleMustBeFuture = 505,

    /// <summary>No dispatch row with that id.</summary>
    DispatchNotFound = 507,

    /// <summary>
    /// The dispatch is in a state that will not take the action asked of it: a
    /// sent one cannot be retried (that would notify the reader twice), and one
    /// still pending cannot be retried either — it has not failed yet.
    /// </summary>
    DispatchNotRetryable = 508,

    /// <summary>Only a pending dispatch can be cancelled; anything else has already happened.</summary>
    DispatchNotCancellable = 509,

    // ── Quran packages ──
    QuranPackageNotFound = 600,

    /// <summary>No package has been published yet, so there is nothing for a device to download.</summary>
    NoPublishedQuranPackage = 601,

    /// <summary>The uploaded bytes do not hash to the checksum the uploader declared.</summary>
    ChecksumMismatch = 602,

    /// <summary>A package with that version number already exists — versions are immutable once uploaded.</summary>
    DuplicateQuranVersion = 603,

    /// <summary>
    /// The canonical Qur'an source is switched off for this deployment, so a
    /// sync was not attempted. Distinct from <see cref="QuranSourceUnreachable"/>
    /// on purpose: one is a setting, the other is a fault, and an admin staring
    /// at a button that did nothing deserves to be told which.
    /// </summary>
    QuranSourceDisabled = 604,

    /// <summary>
    /// The MCP server could not be reached, timed out, or answered with
    /// something that was not the text that was asked for. Never partially
    /// applied — a sync that cannot read every block writes none of them.
    /// </summary>
    QuranSourceUnreachable = 605,

    // ── Home-screen widgets ──

    /// <summary>
    /// Widgets are on, but every kind has been withdrawn — which would leave the
    /// reader a picker with nothing in it. Refused rather than corrected: the
    /// master switch is how you turn widgets off.
    /// </summary>
    WidgetNoKindAllowed = 650,

    /// <summary>No gallery entry with that id.</summary>
    WidgetCatalogItemNotFound = 651,

    /// <summary>
    /// Another gallery entry already claims that key. Keys are the contract
    /// with the app's renderer registry, so two rows holding one key would make
    /// which widget a reader gets depend on row order.
    /// </summary>
    DuplicateWidgetKey = 652,

    /// <summary>
    /// The default design sits outside the range of designs the entry offers,
    /// which would open every fresh install on a design that is not there.
    /// </summary>
    WidgetDefaultDesignOutOfRange = 653,

    /// <summary>
    /// The default names a gallery entry that does not exist, or one that is
    /// hidden. Refused rather than corrected: silently pointing every fresh
    /// install at some other widget is a decision, and it should be the
    /// admin's.
    /// </summary>
    WidgetDefaultUnknown = 654,

    // ── Support desk ──
    FeedbackNotFound = 700,
    FaqNotFound = 701,

    /// <summary>The device already has as many open submissions as the desk will hold.</summary>
    TooManyOpenFeedback = 702,

    // ── Recitations (audio) ──

    /// <summary>No reciter with that id.</summary>
    ReciterNotFound = 800,

    /// <summary>No recording with that id, or it does not belong to that reciter.</summary>
    RecitationNotFound = 801,

    /// <summary>
    /// The recitation publisher is switched off for this deployment, so the
    /// sync was not attempted. A setting, not a fault — see
    /// <see cref="RecitationSourceUnreachable"/> for the fault.
    /// </summary>
    RecitationSourceDisabled = 802,

    /// <summary>
    /// The publisher could not be reached, timed out, or answered with
    /// something that was not a catalogue. Nothing is written: a sync that
    /// could not read the whole list does not apply half of it.
    /// </summary>
    RecitationSourceUnreachable = 803,
}
