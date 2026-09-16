import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { EMPTY, Observable, map, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { environment } from '../../environment';
import { SKIP_AUTH_HANDLING } from '../interceptors/auth-context';
import { IS_SERVER } from '../services/platform';
import {
  AdminCategoryOutput,
  AdminDhikrOutput,
  AdminFaqOutput,
  AdminRadioStationOutput,
  AdminWidgetCatalogOutput,
  ApiLogOutput,
  AppConfigurationInput,
  AppConfigurationOutput,
  AuditOutput,
  AuthOutput,
  BaseResponse,
  BroadcastInput,
  BroadcastOutput,
  CategoryInput,
  DashboardOutput,
  DeviceAdminOutput,
  DeviceInboxOutput,
  DeviceQuery,
  DhikrInput,
  DispatchQuery,
  FaqInput,
  FeedbackOutput,
  FeedbackStatus,
  LanguageInput,
  LanguageOutput,
  PageInput,
  PushDispatchOutput,
  PushOverviewOutput,
  PushRunOutput,
  PageOutput,
  QuranPackageEditInput,
  AdhkarImportOutput,
  TakhrijSyncOutput,
  QuranPackageOutput,
  QuranSyncOutput,
  ReminderInput,
  ReminderOutput,
  SessionOutput,
  SessionQuery,
  StaffInput,
  StaffOutput,
  UiStringsOutput,
  WidgetCatalogInput,
  WidgetSettingsInput,
  WidgetSettingsOutput,
  RadioStationInput,
} from './models';

/**
 * One typed gateway to the API. Components never touch `HttpClient`.
 *
 * Every method returns the `BaseResponse` envelope rather than unwrapping it,
 * because a failure carries an `errorCode` the caller has to map to its own
 * copy — see the `error.*` keys in `i18n/`. Unwrapping here would throw that
 * away and leave every screen with one generic sentence.
 */
@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly isServer = inject(IS_SERVER);
  private readonly base = environment.apiBaseUrl;

  // ─────────────────────────────── auth ───────────────────────────────

  login(email: string, password: string): Observable<BaseResponse<AuthOutput>> {
    return this.post('auth/login', { email, password });
  }

  logout(): Observable<BaseResponse> {
    return this.post('auth/logout', {});
  }

  me(): Observable<BaseResponse<StaffOutput>> {
    return this.get('auth/me');
  }

  // ─────────────────────────────── content ───────────────────────────────

  categories(input: PageInput = {}): Observable<BaseResponse<PageOutput<AdminCategoryOutput>>> {
    return this.get('admin/content/categories', input);
  }

  category(id: number): Observable<BaseResponse<AdminCategoryOutput>> {
    return this.get(`admin/content/categories/${id}`);
  }

  createCategory(input: CategoryInput): Observable<BaseResponse<AdminCategoryOutput>> {
    return this.post('admin/content/categories', input);
  }

  updateCategory(id: number, input: CategoryInput): Observable<BaseResponse<AdminCategoryOutput>> {
    return this.put(`admin/content/categories/${id}`, input);
  }

  /** What importing حصن المسلم's أبواب would add. Writes nothing. */
  previewAdhkarImport(): Observable<BaseResponse<AdhkarImportOutput>> {
    return this.get('admin/content/adhkar/import');
  }

  applyAdhkarImport(): Observable<BaseResponse<AdhkarImportOutput>> {
    return this.post('admin/content/adhkar/import', {});
  }

  /** What filling the drafts' takhrij from حصن المسلم's footnotes would write. */
  previewTakhrijSync(): Observable<BaseResponse<TakhrijSyncOutput>> {
    return this.get('admin/content/adhkar/takhrij');
  }

  /**
   * Fills the attribution of the rows the editor ticked in the check. Publishes
   * nothing — that stays one row at a time, in the editor's own hands.
   */
  applyTakhrijSync(dhikrIds: number[]): Observable<BaseResponse<TakhrijSyncOutput>> {
    return this.post('admin/content/adhkar/takhrij', { dhikrIds });
  }

  deleteCategory(id: number): Observable<BaseResponse> {
    return this.delete(`admin/content/categories/${id}`);
  }

  // ──────────────────────────────── radio ────────────────────────────────

  radioStations(input: PageInput = {}): Observable<BaseResponse<PageOutput<AdminRadioStationOutput>>> {
    return this.get('admin/radio/stations', input);
  }

  createRadioStation(input: RadioStationInput): Observable<BaseResponse<AdminRadioStationOutput>> {
    return this.post('admin/radio/stations', input);
  }

  updateRadioStation(
    id: number,
    input: RadioStationInput,
  ): Observable<BaseResponse<AdminRadioStationOutput>> {
    return this.put(`admin/radio/stations/${id}`, input);
  }

  deleteRadioStation(id: number): Observable<BaseResponse> {
    return this.delete(`admin/radio/stations/${id}`);
  }

  adhkar(
    categoryId: number | null,
    input: PageInput = {},
  ): Observable<BaseResponse<PageOutput<AdminDhikrOutput>>> {
    return this.get('admin/content/adhkar', {
      ...input,
      ...(categoryId ? { categoryId } : {}),
    });
  }

  createDhikr(input: DhikrInput): Observable<BaseResponse<AdminDhikrOutput>> {
    return this.post('admin/content/adhkar', input);
  }

  updateDhikr(id: number, input: DhikrInput): Observable<BaseResponse<AdminDhikrOutput>> {
    return this.put(`admin/content/adhkar/${id}`, input);
  }

  deleteDhikr(id: number): Observable<BaseResponse> {
    return this.delete(`admin/content/adhkar/${id}`);
  }

  /**
   * Publishing is its own call rather than a flag on the update, because it is
   * the one content action a reviewer takes without editing anything — and it
   * gets its own line in the audit trail.
   */
  setDhikrPublished(id: number, published: boolean): Observable<BaseResponse<AdminDhikrOutput>> {
    return this.post(`admin/content/adhkar/${id}/${published ? 'publish' : 'unpublish'}`, {});
  }

  // ─────────────────────────────── languages ───────────────────────────────

  languages(): Observable<BaseResponse<LanguageOutput[]>> {
    return this.get('admin/languages');
  }

  createLanguage(input: LanguageInput): Observable<BaseResponse<LanguageOutput>> {
    return this.post('admin/languages', input);
  }

  updateLanguage(id: number, input: LanguageInput): Observable<BaseResponse<LanguageOutput>> {
    return this.put(`admin/languages/${id}`, input);
  }

  deleteLanguage(id: number): Observable<BaseResponse> {
    return this.delete(`admin/languages/${id}`);
  }

  setDefaultLanguage(id: number): Observable<BaseResponse<LanguageOutput>> {
    return this.post(`admin/languages/${id}/default`, {});
  }

  uiStrings(code: string): Observable<BaseResponse<UiStringsOutput>> {
    return this.get(`admin/languages/${code}/strings`);
  }

  replaceUiStrings(
    code: string,
    strings: Record<string, string>,
  ): Observable<BaseResponse<UiStringsOutput>> {
    return this.put(`admin/languages/${code}/strings`, { strings });
  }

  // ─────────────────────────────── reminders ───────────────────────────────

  reminders(input: PageInput = {}): Observable<BaseResponse<PageOutput<ReminderOutput>>> {
    return this.get('admin/reminders', input);
  }

  createReminder(input: ReminderInput): Observable<BaseResponse<ReminderOutput>> {
    return this.post('admin/reminders', input);
  }

  updateReminder(id: number, input: ReminderInput): Observable<BaseResponse<ReminderOutput>> {
    return this.put(`admin/reminders/${id}`, input);
  }

  deleteReminder(id: number): Observable<BaseResponse> {
    return this.delete(`admin/reminders/${id}`);
  }

  // ─────────────────────────────── broadcasts ───────────────────────────────

  broadcasts(input: PageInput = {}): Observable<BaseResponse<PageOutput<BroadcastOutput>>> {
    return this.get('admin/broadcasts', input);
  }

  createBroadcast(input: BroadcastInput): Observable<BaseResponse<BroadcastOutput>> {
    return this.post('admin/broadcasts', input);
  }

  updateBroadcast(id: number, input: BroadcastInput): Observable<BaseResponse<BroadcastOutput>> {
    return this.put(`admin/broadcasts/${id}`, input);
  }

  sendBroadcast(id: number): Observable<BaseResponse<BroadcastOutput>> {
    return this.post(`admin/broadcasts/${id}/send`, {});
  }

  cancelBroadcast(id: number): Observable<BaseResponse<BroadcastOutput>> {
    return this.post(`admin/broadcasts/${id}/cancel`, {});
  }

  deleteBroadcast(id: number): Observable<BaseResponse> {
    return this.delete(`admin/broadcasts/${id}`);
  }

  // ─────────────────────────────── Qur'an ───────────────────────────────

  quranPackages(): Observable<BaseResponse<QuranPackageOutput[]>> {
    return this.get('admin/quran');
  }

  /**
   * Multipart, and the only upload in the console. The server hashes what it
   * receives and refuses a mismatch — see `docs/BUSINESS_LOGIC.md` §7.1.
   */
  uploadQuranPackage(form: FormData): Observable<BaseResponse<QuranPackageOutput>> {
    return this.http
      .post<BaseResponse<QuranPackageOutput>>(`${this.base}admin/quran`, form)
      .pipe(catchError(() => of(this.networkFailure<QuranPackageOutput>())));
  }

  publishQuranPackage(id: number): Observable<BaseResponse<QuranPackageOutput>> {
    return this.post(`admin/quran/${id}/publish`, {});
  }

  /**
   * Makes this package's edition the one a device is given when it names none.
   *
   * Separate from publishing because the two are different decisions: publishing
   * puts a mushaf on the shelf, this one points every fresh install at it.
   */
  setDefaultQuranPackage(id: number): Observable<BaseResponse<QuranPackageOutput>> {
    return this.post(`admin/quran/${id}/default`, {});
  }

  /**
   * Withdraws the published package, leaving nothing published.
   *
   * Without it, publishing was a one-way door: a package could only be replaced
   * by another, and a published one cannot be deleted — so a mushaf found to
   * have a defect had no way out at all.
   */
  unpublishQuranPackage(id: number): Observable<BaseResponse<QuranPackageOutput>> {
    return this.post(`admin/quran/${id}/unpublish`, {});
  }

  /** Corrects what a package says about itself. Never its bytes, version or checksum. */
  editQuranPackage(
    id: number,
    input: QuranPackageEditInput,
  ): Observable<BaseResponse<QuranPackageOutput>> {
    return this.put(`admin/quran/${id}`, input);
  }

  /**
   * Downloads a stored package, published or not.
   *
   * Fetched as a blob rather than handed to `window.open`: the endpoint is
   * behind `[AppAuthorize]`, and a new tab carries no Authorization header — it
   * would arrive unauthenticated and 401 every time. Going through `HttpClient`
   * means the auth interceptor attaches the token like it does everywhere else.
   */
  downloadQuranPackage(id: number): Observable<Blob | null> {
    return this.http
      .get(`${this.base}admin/quran/${id}/download`, { responseType: 'blob' })
      .pipe(catchError(() => of(null)));
  }

  deleteQuranPackage(id: number): Observable<BaseResponse> {
    return this.delete(`admin/quran/${id}`);
  }

  /** What a sync against the canonical Qur'an source would change. Writes nothing. */
  previewQuranAthkarSync(): Observable<BaseResponse<QuranSyncOutput>> {
    return this.get('admin/quran/athkar-sync');
  }

  applyQuranAthkarSync(): Observable<BaseResponse<QuranSyncOutput>> {
    return this.post('admin/quran/athkar-sync', {});
  }

  // ─────────────────────────── home-screen widget ───────────────────────────

  pushOverview(windowDays = 7): Observable<BaseResponse<PushOverviewOutput>> {
    return this.get('admin/push/overview', { windowDays });
  }

  /**
   * Composes and queues a message in one call.
   *
   * Not `createBroadcast` + `sendBroadcast` from the screen: two round trips
   * can leave a draft stranded between them, and the server already does both
   * under one action.
   */
  pushSend(input: BroadcastInput): Observable<BaseResponse<BroadcastOutput>> {
    return this.post('admin/push/send', input);
  }

  widgetSettings(): Observable<BaseResponse<WidgetSettingsOutput>> {
    return this.get('admin/widget');
  }

  updateWidgetSettings(input: WidgetSettingsInput): Observable<BaseResponse<WidgetSettingsOutput>> {
    return this.put('admin/widget', input);
  }

  widgetCatalog(): Observable<BaseResponse<AdminWidgetCatalogOutput[]>> {
    return this.get('admin/widget/catalog');
  }

  createWidgetCatalogItem(
    input: WidgetCatalogInput,
  ): Observable<BaseResponse<AdminWidgetCatalogOutput>> {
    return this.post('admin/widget/catalog', input);
  }

  updateWidgetCatalogItem(
    id: number,
    input: WidgetCatalogInput,
  ): Observable<BaseResponse<AdminWidgetCatalogOutput>> {
    return this.put(`admin/widget/catalog/${id}`, input);
  }

  reorderWidgetCatalog(ids: number[]): Observable<BaseResponse> {
    return this.put('admin/widget/catalog/reorder', { ids });
  }

  deleteWidgetCatalogItem(id: number): Observable<BaseResponse> {
    return this.delete(`admin/widget/catalog/${id}`);
  }

  // ─────────────────────────────── support ───────────────────────────────

  faq(input: PageInput = {}): Observable<BaseResponse<PageOutput<AdminFaqOutput>>> {
    return this.get('admin/support/faq', input);
  }

  createFaq(input: FaqInput): Observable<BaseResponse<AdminFaqOutput>> {
    return this.post('admin/support/faq', input);
  }

  updateFaq(id: number, input: FaqInput): Observable<BaseResponse<AdminFaqOutput>> {
    return this.put(`admin/support/faq/${id}`, input);
  }

  deleteFaq(id: number): Observable<BaseResponse> {
    return this.delete(`admin/support/faq/${id}`);
  }

  feedback(
    status: FeedbackStatus | null,
    input: PageInput = {},
  ): Observable<BaseResponse<PageOutput<FeedbackOutput>>> {
    return this.get('admin/support/feedback', {
      ...input,
      ...(status === null ? {} : { status }),
    });
  }

  replyToFeedback(
    id: number,
    reply: string,
    status: FeedbackStatus,
  ): Observable<BaseResponse<FeedbackOutput>> {
    return this.post(`admin/support/feedback/${id}/reply`, { reply, status });
  }

  // ─────────────────────────────── platform ───────────────────────────────

  dashboard(): Observable<BaseResponse<DashboardOutput>> {
    return this.get('admin/dashboard');
  }

  configuration(): Observable<BaseResponse<AppConfigurationOutput>> {
    return this.get('admin/configuration');
  }

  /**
   * The anonymous read of the same row, used to paint the brand before anybody
   * signs in. Opted out of the auth interceptors: a 401 here would end a
   * session that does not exist yet.
   */
  publicConfiguration(): Observable<BaseResponse<AppConfigurationOutput>> {
    return this.http
      .get<BaseResponse<AppConfigurationOutput>>(`${this.base}configuration`, {
        context: new HttpContext().set(SKIP_AUTH_HANDLING, true),
      })
      .pipe(catchError(() => of(this.networkFailure<AppConfigurationOutput>())));
  }

  updateConfiguration(
    input: AppConfigurationInput,
  ): Observable<BaseResponse<AppConfigurationOutput>> {
    return this.put('admin/configuration', input);
  }

  staff(input: PageInput = {}): Observable<BaseResponse<PageOutput<StaffOutput>>> {
    return this.get('admin/staff', input);
  }

  createStaff(input: StaffInput): Observable<BaseResponse<StaffOutput>> {
    return this.post('admin/staff', input);
  }

  updateStaff(id: number, input: StaffInput): Observable<BaseResponse<StaffOutput>> {
    return this.put(`admin/staff/${id}`, input);
  }

  deleteStaff(id: number): Observable<BaseResponse> {
    return this.delete(`admin/staff/${id}`);
  }

  audit(
    action: string | null,
    input: PageInput = {},
  ): Observable<BaseResponse<PageOutput<AuditOutput>>> {
    return this.get('admin/audit', { ...input, ...(action ? { action } : {}) });
  }

  logs(
    statusCode: number | null,
    input: PageInput = {},
  ): Observable<BaseResponse<PageOutput<ApiLogOutput>>> {
    return this.get('admin/logs', { ...input, ...(statusCode ? { statusCode } : {}) });
  }

  // ──────────────────────── installs and staff sessions ────────────────────

  /**
   * The installs.
   *
   * There is no personal data on these rows — a key the app minted for itself,
   * a platform, a language, a zone and a date — and the push token is not
   * returned at all, only its last eight characters.
   */
  devices(input: DeviceQuery = {}): Observable<BaseResponse<PageOutput<DeviceAdminOutput>>> {
    return this.get('admin/devices', input);
  }

  device(id: number): Observable<BaseResponse<DeviceAdminOutput>> {
    return this.get(`admin/devices/${id}`);
  }

  /** What actually reached one install's inbox — as against what was attempted. */
  deviceInbox(
    id: number,
    input: PageInput = {},
  ): Observable<BaseResponse<PageOutput<DeviceInboxOutput>>> {
    return this.get(`admin/devices/${id}/inbox`, input);
  }

  /**
   * The whole FCM token for one install.
   *
   * A POST because it is an action with a consequence: the server writes it to
   * the audit trail. One install at a time, deliberately.
   */
  revealPushToken(id: number): Observable<BaseResponse<string>> {
    return this.post(`admin/devices/${id}/push-token`, {});
  }

  /** Staff sessions — who is signed in to this console. */
  sessions(input: SessionQuery = {}): Observable<BaseResponse<PageOutput<SessionOutput>>> {
    return this.get('admin/sessions', input);
  }

  revokeSession(id: number): Observable<BaseResponse> {
    return this.delete(`admin/sessions/${id}`);
  }

  /** Sign one member of staff out everywhere. Returns how many sessions ended. */
  revokeAllSessionsFor(userId: number): Observable<BaseResponse<number>> {
    return this.delete(`admin/sessions/user/${userId}`);
  }

  // ──────────────────────────── the delivery log ───────────────────────────

  dispatches(
    input: DispatchQuery = {},
  ): Observable<BaseResponse<PageOutput<PushDispatchOutput>>> {
    return this.get('admin/push/dispatches', input);
  }

  retryDispatch(id: number): Observable<BaseResponse<PushDispatchOutput>> {
    return this.post(`admin/push/dispatches/${id}/retry`, {});
  }

  cancelDispatch(id: number): Observable<BaseResponse<PushDispatchOutput>> {
    return this.post(`admin/push/dispatches/${id}/cancel`, {});
  }

  /**
   * Runs the pipeline now rather than waiting for the workers.
   *
   * Safe to press twice: it is the same dispatcher the workers call, and every
   * pass of it is idempotent by design.
   */
  runPushNow(): Observable<BaseResponse<PushRunOutput>> {
    return this.post('admin/push/run', {});
  }

  // ─────────────────────────────── plumbing ───────────────────────────────

  /**
   * `object` rather than `Record<string, unknown>`: the callers pass typed
   * shapes like `PageInput`, and an interface without an index signature is not
   * assignable to a Record. Widening here keeps the call sites typed.
   */
  private get<T>(path: string, query: object = {}): Observable<BaseResponse<T>> {
    // The access token lives in `localStorage`, which the render server does
    // not have — so every admin fetch during SSR goes out unauthenticated,
    // comes back 401, and is thrown away when the browser refetches it with a
    // token a moment later. That is a wasted round trip per panel, and it fills
    // the CMS's own request log with 401s that are not auth problems, which is
    // the log an admin would search when there *is* one.
    if (this.isServer && path.startsWith('admin/')) return EMPTY;

    let params = new HttpParams();

    for (const [key, value] of Object.entries(query)) {
      if (value !== null && value !== undefined && value !== '') {
        params = params.set(key, String(value));
      }
    }

    return this.http
      .get<BaseResponse<T>>(`${this.base}${path}`, { params })
      .pipe(catchError(() => of(this.networkFailure<T>())));
  }

  private post<T>(path: string, body: unknown): Observable<BaseResponse<T>> {
    return this.http
      .post<BaseResponse<T>>(`${this.base}${path}`, body)
      .pipe(catchError(() => of(this.networkFailure<T>())));
  }

  private put<T>(path: string, body: unknown): Observable<BaseResponse<T>> {
    return this.http
      .put<BaseResponse<T>>(`${this.base}${path}`, body)
      .pipe(catchError(() => of(this.networkFailure<T>())));
  }

  private delete<T>(path: string): Observable<BaseResponse<T>> {
    return this.http
      .delete<BaseResponse<T>>(`${this.base}${path}`)
      .pipe(catchError(() => of(this.networkFailure<T>())));
  }

  /**
   * A transport failure, shaped as the envelope.
   *
   * So a component never has to tell "the server said no" apart from "the
   * server did not answer" — both arrive as `success: false` with a code it
   * already knows how to render. `errorCode: 1` is `UnknownError` on the
   * server side.
   */
  private networkFailure<T>(): BaseResponse<T> {
    return { success: false, errorCode: 1, message: null, errors: [] };
  }
}

/** Maps a server `ErrorCode` to a key in the CMS's own copy. */
export function errorKey(code: number): string {
  switch (code) {
    case 2:
      return 'error.validation';
    case 3:
      return 'error.notFound';
    case 5:
      return 'error.forbidden';
    case 104:
      return 'error.lastAdministrator';
    case 302:
      return 'error.duplicateKey';
    case 304:
      return 'error.sourceRequired';
    case 305:
      return 'error.categoryNotEmpty';
    case 306:
      return 'categories.import.missing';
    case 307:
      return 'categories.takhrij.missing';
    case 308:
      return 'radio.notFound';
    case 309:
      return 'radio.insecureStream';
    case 402:
      return 'error.defaultLanguage';
    case 403:
      return 'error.sourceLanguage';
    case 504:
      return 'error.broadcastAlreadySent';
    case 505:
      return 'error.scheduleMustBeFuture';
    case 602:
      return 'error.checksumMismatch';
    case 603:
      return 'error.duplicateQuranVersion';
    case 604:
      return 'quran.sync.disabled';
    case 605:
      return 'quran.sync.unreachable';
    case 105:
      return 'sessions.notFound';
    case 106:
      return 'sessions.cannotRevokeHigher';
    case 507:
      return 'push.log.notFound';
    case 508:
      return 'push.log.notRetryable';
    case 509:
      return 'push.log.notCancellable';
    case 651:
      return 'widget.catalog.notFound';
    case 652:
      return 'widget.catalog.duplicateKey';
    case 653:
      return 'widget.catalog.defaultOutOfRange';
    case 654:
      return 'widget.defaultUnknown';
    default:
      return 'error.generic';
  }
}
