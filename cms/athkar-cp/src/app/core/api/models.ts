/**
 * The API contract, as TypeScript.
 *
 * The enums mirror `Shareds/Enums` on the server and `models/models.dart` in
 * the app. **The numbers are the contract** — reordering a member here silently
 * re-labels data in all three stacks.
 */

// ─────────────────────────────── envelope ───────────────────────────────

export interface BaseResponse<T = unknown> {
  success: boolean;
  errorCode: number;
  message: string | null;
  errors: string[];
  data?: T;
}

export interface PageOutput<T> {
  data: T[];
  totalRows: number;
}

export interface PageInput {
  pageNumber?: number;
  pageSize?: number;
  search?: string;
}

// ─────────────────────────────── enums ───────────────────────────────

export enum Roles {
  Editor = 1,
  Admin = 2,
  SuperAdmin = 3,
}

export enum DevicePlatform {
  Unknown = 0,
  Android = 1,
  Ios = 2,
  Web = 3,
}

export enum HadithGrade {
  Sahih = 1,
  Hasan = 2,
  SahihLighayrihi = 3,
  HasanLighayrihi = 4,
  QuranVerse = 5,
  MuttafaqAlayh = 6,
}

export enum CategoryRhythm {
  None = 0,
  Daily = 1,
  Monthly = 2,
}

export enum PrayerAnchor {
  None = 0,
  Fajr = 1,
  Sunrise = 2,
  Dhuhr = 3,
  Asr = 4,
  Maghrib = 5,
  Isha = 6,
  Bedtime = 7,
  IslamicMidnight = 8,
  LastThirdOfNight = 9,
}

export enum ReminderKind {
  FixedTime = 1,
  PrayerAnchored = 2,
}

/**
 * Which side raises a reminder. The one setting that decides whether a campaign
 * can be prayer-anchored — see `docs/BUSINESS_LOGIC.md` §5.
 */
export enum ReminderDelivery {
  ServerPush = 1,
  DeviceLocal = 2,
}

export enum WeekDays {
  None = 0,
  Sunday = 1,
  Monday = 2,
  Tuesday = 4,
  Wednesday = 8,
  Thursday = 16,
  Friday = 32,
  Saturday = 64,
  All = 127,
}

export enum Audience {
  All = 0,
  Language = 1,
  Platform = 2,
  Device = 3,
}

export enum BroadcastStatus {
  Draft = 0,
  Scheduled = 1,
  Sending = 2,
  Sent = 3,
  Failed = 4,
  Cancelled = 5,
}

export enum FeedbackKind {
  Suggestion = 1,
  Complaint = 2,
  Correction = 3,
  Praise = 4,
}

export enum FeedbackStatus {
  New = 0,
  InProgress = 1,
  Answered = 2,
  Closed = 3,
}

export enum FaqCategory {
  General = 0,
  Adhkar = 1,
  PrayerTimes = 2,
  Notifications = 3,
  Qibla = 4,
  Quran = 5,
  Privacy = 6,
}

export enum CalculationMethod {
  UmmAlQura = 1,
  MuslimWorldLeague = 2,
  Egyptian = 3,
  Karachi = 4,
  Kuwait = 5,
  Qatar = 6,
  Dubai = 7,
  Turkey = 8,
  NorthAmerica = 9,
  Singapore = 10,
  Tehran = 11,
  MoonsightingCommittee = 12,

  /** دائرة الإفتاء العام الأردنية — Fajr 18°, Isha 18°. Appended, never reordered. */
  Jordan = 13,
}

export enum Madhab {
  Standard = 1,
  Hanafi = 2,
}

export enum WidgetKind {
  NextPrayer = 1,
  Dhikr = 2,
  Combined = 3,
}

export enum WidgetTheme {
  System = 0,
  Light = 1,
  Dark = 2,
  Transparent = 3,
}

/** Mirrors `Shareds/Enums/WidgetSurface.cs`. */
export enum WidgetSurface {
  Home = 1,
  Lock = 2,
}

/** Mirrors `Shareds/Enums/WidgetFamily.cs` — a grouping label, not a layout. */
export enum WidgetFamily {
  Prayer = 1,
  Date = 2,
  PrayerAndDate = 3,
  Dhikr = 4,
  Quran = 5,
  Countdown = 6,
  Moon = 7,
  Tracker = 8,
}

export enum QuranScript {
  Uthmani = 1,
  IndoPak = 2,
  Naskh = 3,
}

// ─────────────────────────────── auth ───────────────────────────────

export interface StaffOutput {
  id: number;
  email: string;
  fullName: string;
  languageCode: string;
  isActive: boolean;
  lastLoginAt: string | null;
  roles: Roles[];
}

export interface AuthOutput {
  accessToken: string;
  accessExpiresAt: string;
  refreshToken: string;
  refreshExpiresAt: string;
  user: StaffOutput;
}

export interface StaffInput {
  email: string;
  fullName: string;
  password?: string | null;
  languageCode: string;
  isActive: boolean;
  roles: Roles[];
}

// ─────────────────────────────── content ───────────────────────────────

/**
 * One language's wording for anything translatable.
 *
 * The same shape carries a category name, a dhikr translation, a reminder's
 * title and an FAQ answer — `title` plus an optional `body`, and two fields only
 * a dhikr uses. One editor component then serves every one of them.
 */
export interface TranslationInput {
  languageCode: string;
  title: string;
  body?: string | null;
  /** A dhikr's transliteration. Unused elsewhere. */
  secondary?: string | null;
  /** A dhikr's narrated virtue. Unused elsewhere. */
  virtue?: string | null;
}

export interface AdminCategoryOutput {
  id: number;
  key: string;
  icon: string | null;
  sortOrder: number;
  rhythm: CategoryRhythm;
  anchor: PrayerAnchor;
  isPublished: boolean;
  dhikrCount: number;
  publishedDhikrCount: number;
  translatedLanguages: string[];
  translations: TranslationInput[];
}

export interface CategoryInput {
  key: string;
  icon?: string | null;
  sortOrder: number;
  rhythm: CategoryRhythm;
  anchor: PrayerAnchor;
  isPublished: boolean;
  translations: TranslationInput[];
}

export interface AdminDhikrOutput {
  id: number;
  categoryId: number;
  categoryKey: string;
  sortOrder: number;
  arabicText: string;
  repeatCount: number;
  sourceBook: string | null;
  sourceReference: string | null;
  grade: HadithGrade | null;
  gradedBy: string | null;
  isPublished: boolean;
  /** False when the row has no source, which is what blocks publishing. */
  isPublishable: boolean;
  translatedLanguages: string[];
  translations: TranslationInput[];
}

export interface DhikrInput {
  categoryId: number;
  sortOrder: number;
  arabicText: string;
  repeatCount: number;
  sourceBook?: string | null;
  sourceReference?: string | null;
  grade?: HadithGrade | null;
  gradedBy?: string | null;
  isPublished: boolean;
  translations: TranslationInput[];
}

// ─────────────────────────────── localisation ───────────────────────────────

export interface LanguageOutput {
  id: number;
  code: string;
  nativeName: string;
  englishName: string;
  isRtl: boolean;
  isEnabled: boolean;
  isDefault: boolean;
  sortOrder: number;
  version: number;
  stringCount: number;
}

export interface LanguageInput {
  code: string;
  nativeName: string;
  englishName: string;
  isRtl: boolean;
  isEnabled: boolean;
  sortOrder: number;
}

export interface UiStringsOutput {
  languageCode: string;
  version: number;
  strings: Record<string, string>;
}

// ─────────────────────────────── reminders ───────────────────────────────

export interface ReminderOutput {
  id: number;
  key: string;
  categoryId: number | null;
  categoryKey: string | null;
  kind: ReminderKind;
  delivery: ReminderDelivery;
  localTime: string | null;
  anchor: PrayerAnchor;
  offsetMinutes: number;
  days: number;
  audience: Audience;
  targetLanguageCode: string | null;
  targetPlatform: DevicePlatform | null;
  androidChannelId: string;
  isEnabled: boolean;
  isUserAdjustable: boolean;
  version: number;
  translations: TranslationInput[];
}

export interface ReminderInput {
  key: string;
  categoryId?: number | null;
  kind: ReminderKind;
  delivery: ReminderDelivery;
  localTime?: string | null;
  anchor: PrayerAnchor;
  offsetMinutes: number;
  days: number;
  audience: Audience;
  targetLanguageCode?: string | null;
  targetPlatform?: DevicePlatform | null;
  isEnabled: boolean;
  isUserAdjustable: boolean;
  translations: TranslationInput[];
}

// ─────────────────────────────── broadcasts ───────────────────────────────

export interface BroadcastOutput {
  id: number;
  status: BroadcastStatus;
  audience: Audience;
  targetLanguageCode: string | null;
  targetPlatform: DevicePlatform | null;
  targetDeviceKey: string | null;
  scheduledAtUtc: string | null;
  sentAtUtc: string | null;
  route: string | null;
  recipientCount: number;
  sentCount: number;
  failedCount: number;
  skippedCount: number;
  createdAt: string;
  translations: TranslationInput[];
}

export interface BroadcastInput {
  audience: Audience;
  targetLanguageCode?: string | null;
  targetPlatform?: DevicePlatform | null;
  targetDeviceKey?: string | null;
  scheduledAtUtc?: string | null;
  route?: string | null;
  translations: TranslationInput[];
}

// ─────────────────────────────── Qur'an ───────────────────────────────

export interface QuranPackageOutput {
  id: number;

  /**
   * Which mushaf this file is a version of — the slug a reader's device stores
   * its copy under. Stable across re-uploads, which is why it is not the id.
   */
  edition: string;

  /** What the reader sees in the app's list of mushafs. */
  name: string;

  /** Whether a device that names no edition is given this one. Exactly one carries it. */
  isDefault: boolean;

  version: number;
  fileName: string;
  sizeBytes: number;
  sha256: string;
  script: QuranScript;
  hasWaqfAnnotations: boolean;
  releaseNotes: string | null;
  isPublished: boolean;
  publishedAt: string | null;
  downloadCount: number;
  uploadedAt: string;
}

/**
 * What an import of حصن المسلم's أبواب did, or — in preview — would do.
 *
 * `draftsAwaitingSource` is the number that matters: the source carries no
 * takhrij, so every dhikr lands unpublished and somebody has to attribute it
 * before a reader ever sees it.
 */
export interface AdhkarImportOutput {
  applied: boolean;
  source: string;
  chaptersChecked: number;
  chaptersAdded: number;
  adhkarAdded: number;
  adhkarAlreadyPresent: number;
  chaptersSkipped: number;
  contentVersion: number | null;
  draftsAwaitingSource: number;
  chapters: AdhkarImportChapter[];
}

export interface AdhkarImportChapter {
  key: string;
  title: string;
  sourceId: number;
  categoryId: number | null;
  action: AdhkarImportAction;
  adding: number;
  present: number;
}

export interface TakhrijSyncOutput {
  applied: boolean;
  source: string;
  draftsMissingSource: number;
  matched: number;
  publishable: number;
  graded: number;
  filled: number;
  unmatched: number;
  alreadyAttributed: number;
  disagreements: number;
  published: number;
  rows: TakhrijSyncRow[];
}

export interface TakhrijSyncRow {
  dhikrId: number;
  excerpt: string;
  categoryKey: string;
  book: string | null;
  reference: string | null;
  grade: number | null;
  gradedBy: string | null;
  note: string | null;
  chosen: boolean;
  status: TakhrijSyncStatus;
}

export enum TakhrijSyncStatus {
  Filled = 0,
  BookOnly = 1,
  Unmatched = 2,
  AlreadyAttributed = 3,
  Disagreement = 4,
}

export enum AdhkarImportAction {
  Unchanged = 0,
  Added = 1,
  Extended = 2,
  Skipped = 3,
}

/**
 * What a sync against the canonical Qur'an source did, or — in preview — what
 * it would do. The server never deletes and never touches a row an editor has
 * re-attributed, so `skipped` is a report, not a failure.
 */
export interface QuranSyncOutput {
  applied: boolean;
  source: string;
  checked: number;
  added: number;
  changed: number;
  skipped: number;
  contentVersion: number | null;
  changes: QuranSyncChange[];
}

export interface QuranSyncChange {
  key: string;
  reference: string;
  name: string;
  dhikrId: number | null;
  action: QuranSyncAction;
  /** Which fields differ: 'arabic', 'search', 'en'. Empty for an addition. */
  fields: string[];
  storedArabic: string | null;
  canonicalArabic: string | null;
}

export enum QuranSyncAction {
  Unchanged = 0,
  Added = 1,
  Changed = 2,
  Skipped = 3,
}

// ─────────────────────────── home-screen widget ───────────────────────────

export interface WidgetSettingsOutput {
  isEnabled: boolean;
  defaultKind: WidgetKind;
  allowPrayerWidget: boolean;
  allowDhikrWidget: boolean;
  theme: WidgetTheme;
  refreshMinutes: number;
  showHijriDate: boolean;
  showCountdown: boolean;
  /** Null means the widget follows the time of day. */
  dhikrCategoryId: number | null;

  /**
   * The gallery entry a reader gets before they have chosen one, by
   * `WidgetCatalogOutput.key`.
   *
   * A key rather than an id: the app resolves it against its own renderer
   * registry, and a build that has never heard of it falls back rather than
   * failing. Null means "whatever that app can draw first".
   */
  defaultWidgetKey: string | null;

  /**
   * What the reader may change for themselves.
   *
   * Permissions, not preferences: the reader's own choice lives on their phone
   * and never reaches this server. They exist because a customisation can break
   * the promise the widget makes — a transparent panel over a photographic
   * wallpaper can render a dhikr unreadable — so there is a way to withdraw one
   * without shipping an app update.
   */
  allowBackgroundColor: boolean;
  allowTransparency: boolean;
  allowBackgroundImage: boolean;
  allowCustomWidget: boolean;
  customWidgetMaxLength: number;

  version: number;
}

export type WidgetSettingsInput = Omit<WidgetSettingsOutput, 'version'>;

// ──────────────────────────── the widget gallery ────────────────────────────

/**
 * One entry in the gallery the reader browses.
 *
 * Note what is *not* here: a design. A widget is drawn by code compiled into
 * the app, so nothing typed in this console becomes a new layout on a phone
 * that already shipped. [key] names a renderer the app already holds, and an
 * app that has never heard of a key skips it — which is what lets the gallery
 * be edited here and still reach an install from last year.
 */
export interface AdminWidgetCatalogOutput {
  id: number;
  key: string;
  surface: WidgetSurface;
  family: WidgetFamily;

  /** A cap on the app's own design count, never a promise of that many. */
  designCount: number;
  defaultDesign: number;

  isExclusive: boolean;
  isNew: boolean;
  isEnabled: boolean;
  sortOrder: number;

  translations: WidgetCatalogTranslationOutput[];
}

export interface WidgetCatalogTranslationOutput {
  languageCode: string;
  title: string;
  subtitle: string | null;
}

export interface WidgetCatalogInput {
  /** Only read on create — an existing entry's key is immutable. */
  key: string;
  surface: WidgetSurface;
  family: WidgetFamily;
  designCount: number;
  defaultDesign: number;
  isExclusive: boolean;
  isNew: boolean;
  isEnabled: boolean;
  sortOrder: number;

  /** Reuses the one translations editor, so `body` is the subtitle. */
  translations: TranslationInput[];
}

// ─────────────────────────────── support ───────────────────────────────

export interface AdminFaqOutput {
  id: number;
  category: FaqCategory;
  sortOrder: number;
  isPublished: boolean;
  translatedLanguages: string[];
  translations: TranslationInput[];
}

export interface FaqInput {
  category: FaqCategory;
  sortOrder: number;
  isPublished: boolean;
  translations: TranslationInput[];
}

export interface FeedbackOutput {
  id: number;
  kind: FeedbackKind;
  status: FeedbackStatus;
  message: string;
  dhikrId: number | null;
  dhikrText: string | null;
  contactEmail: string | null;
  appVersion: string | null;
  languageCode: string;
  reply: string | null;
  repliedAt: string | null;
  repliedByName: string | null;
  createdAt: string;
}

// ─────────────────────────────── platform ───────────────────────────────

export interface AppConfigurationOutput {
  primaryColor: string;
  defaultCalculationMethod: CalculationMethod;
  defaultMadhab: Madhab;
  morningOffsetMinutes: number;
  eveningOffsetMinutes: number;
  contentVersion: number;
  minimumAppBuild: number;
  reviewingScholar: string | null;
  reviewingScholarCredential: string | null;
  supportEmail: string | null;
  supportWebsite: string | null;
  privacyPolicyUrl: string | null;
  androidPackageName: string | null;
  iosAppStoreId: string | null;
  updatedAt: string;
}

export type AppConfigurationInput = Omit<
  AppConfigurationOutput,
  'contentVersion' | 'updatedAt'
>;

export interface DailyCount {
  day: string;
  count: number;
}

export interface DashboardOutput {
  totalDevices: number;
  activeDevices: number;
  reachableDevices: number;
  publishedCategories: number;
  publishedAdhkar: number;
  /** Drafts with no source — the backlog between the corpus and publication. */
  unsourcedDrafts: number;
  enabledLanguages: number;
  activeReminders: number;
  openFeedback: number;
  pushLastDay: Record<string, number>;
  installs: DailyCount[];
  devicesByPlatform: Record<string, number>;
  devicesByLanguage: Record<string, number>;
}

export interface AuditOutput {
  id: number;
  action: string;
  entityName: string;
  entityId: number | null;
  userName: string | null;
  oldValue: string | null;
  newValue: string | null;
  createdAt: string;
}

export interface ApiLogOutput {
  id: number;
  method: string;
  path: string;
  statusCode: number;
  errorCode: number | null;
  durationMs: number;
  deviceKey: string | null;
  userId: number | null;
  createdAt: string;
}

// ───────────────────────────── push manager ─────────────────────────────

/** Mirrors `Areas/Services/Notifications/Models/PushManagerModels.cs`. */
export interface PushReachOutput {
  totalDevices: number;
  reachable: number;
  muted: number;
  tokenless: number;
  stale: number;
  reachableByPlatform: Record<string, number>;
  reachableByLanguage: Record<string, number>;
}

export interface PushQueueOutput {
  pendingDispatches: number;
  overdueDispatches: number;
  nextDispatchAtUtc: string | null;
  scheduledBroadcasts: number;
  nextBroadcastAtUtc: string | null;
  activePushCampaigns: number;
}

export interface PushOutcomesOutput {
  windowDays: number;
  sent: number;
  failed: number;
  skipped: number;
  tokenExpired: number;
  deliveryRate: number;
}

export interface PushFailureOutput {
  id: number;
  scheduledAtUtc: string;
  status: string;
  attempts: number;
  error: string | null;
  source: string;
  platform: string;
  languageCode: string;
}

export interface PushOverviewOutput {
  reach: PushReachOutput;
  queue: PushQueueOutput;
  outcomes: PushOutcomesOutput;
  isFcmConfigured: boolean;
  recentFailures: PushFailureOutput[];
}

// ─────────────────────────── installs and sessions ───────────────────────────

/**
 * Why a broadcast would or would not land on an install.
 *
 * Derived on the server from three columns and never stored, so unlike every
 * other enum here it is not a wire contract — it is a conclusion, and the
 * server draws it so this screen and the push manager's four columns cannot
 * disagree about what a given install is.
 */
export enum DeviceReach {
  Tokenless = 0,
  Muted = 1,
  Silent = 2,
  Reachable = 3,
}

export interface DeviceAdminOutput {
  id: number;
  deviceKey: string;
  platform: DevicePlatform;
  languageCode: string;
  timeZoneId: string;
  countryCode: string | null;
  appVersion: string | null;
  hasPushToken: boolean;

  /** The last eight characters of the FCM token. The whole one is an audited read. */
  pushTokenTail: string | null;

  notificationsEnabled: boolean;
  lastSeenAt: string;
  firstSeenAt: string;
  syncedContentVersion: number;
  reach: DeviceReach;
  unreadCount: number;
}

export interface DeviceInboxOutput {
  id: number;
  kind: NotificationKind;
  title: string;
  body: string;
  languageCode: string;
  route: string | null;
  createdAt: string;
  readAt: string | null;
}

/**
 * What an inbox row is.
 *
 * The numbers are the contract — `Shareds/Enums/NotificationKind.cs` and the
 * app's `models.dart` hold the same three, and they start at 1. Writing them
 * from zero here silently re-labelled every row one place down the list.
 */
export enum NotificationKind {
  Reminder = 1,
  Broadcast = 2,
  System = 3,
}

/** One staff session. Neither the session key nor the refresh token is ever here. */
export interface SessionOutput {
  id: number;
  userId: number;
  userName: string;
  email: string;
  roles: Roles[];

  /** As the browser reported it — untrusted text, rendered as text and never as markup. */
  userAgent: string | null;

  ipAddress: string | null;
  signedInAt: string;
  lastUsedAt: string;
  refreshExpiresAt: string;
  isExpired: boolean;

  /** The session making this very request. Revocable, but never by accident. */
  isCurrent: boolean;
}

// ──────────────────────────── the delivery log ───────────────────────────────

export enum PushStatus {
  Pending = 0,
  Sent = 1,
  Failed = 2,
  Skipped = 3,
  TokenExpired = 4,
}

export interface PushDispatchOutput {
  id: number;
  scheduledAtUtc: string;
  sentAtUtc: string | null;
  status: PushStatus;
  attempts: number;
  messageId: string | null;
  error: string | null;
  source: string;
  campaignId: number | null;
  broadcastId: number | null;
  title: string | null;
  deviceId: number;
  deviceKey: string;
  platform: DevicePlatform;
  languageCode: string;

  /** Decided by the server. The screen reads the rule rather than re-deriving it. */
  canRetry: boolean;
  canCancel: boolean;
}

/** What one manual run of the pipeline did. */
export interface PushRunOutput {
  materialised: number;
  broadcastsStarted: number;
  attempted: number;
}

/** What an admin may correct on a mushaf package. Never its bytes or its version. */
export interface QuranPackageEditInput {
  name: string;
  script: QuranScript;
  hasWaqfAnnotations: boolean;
  releaseNotes: string | null;
}

// ───────────────────────── query shapes for the above ────────────────────────

/**
 * Filters for the installs list.
 *
 * Each one is a state the push manager counts, because the reason to open this
 * screen is almost always a number on that one — «٧ بلا رمز» is a sentence
 * until it is seven rows.
 */
export interface DeviceQuery extends PageInput {
  platform?: DevicePlatform;
  languageCode?: string;
  hasPushToken?: boolean;
  notificationsEnabled?: boolean;
  isActive?: boolean;
}

export interface SessionQuery extends PageInput {
  userId?: number;

  /** Sessions past their refresh window. Off by default: the screen asks who can act now. */
  includeExpired?: boolean;
}

export interface DispatchQuery extends PageInput {
  status?: PushStatus;
  deviceKey?: string;
  platform?: DevicePlatform;
  languageCode?: string;
  broadcastId?: number;
  campaignId?: number;
  fromUtc?: string;
  toUtc?: string;
}
