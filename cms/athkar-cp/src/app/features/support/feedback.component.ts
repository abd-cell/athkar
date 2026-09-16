import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { ApiService, errorKey } from '../../core/api/api.service';
import { FeedbackKind, FeedbackOutput, FeedbackStatus } from '../../core/api/models';
import { AppDatePipe } from '../../core/pipes/app-date.pipe';
import { TranslatePipe } from '../../core/pipes/translate.pipe';

/**
 * The support desk.
 *
 * Corrections come first — the server sorts them there — because a reader
 * telling us a grading is wrong is the most valuable message this project gets,
 * and it should not sit behind a week of thanks.
 *
 * There is no email to reply to: the answer is delivered as a notification to
 * the device that wrote in. That is the cost of having no accounts, and the
 * hint under the field says so rather than pretending otherwise.
 */
@Component({
  selector: 'app-feedback',
  imports: [FormsModule, AppDatePipe, TranslatePipe],
  templateUrl: './feedback.component.html',
})
export class FeedbackComponent {
  private readonly api = inject(ApiService);

  protected readonly rows = signal<FeedbackOutput[]>([]);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly filter = signal<FeedbackStatus | null>(null);
  protected readonly replyingTo = signal<FeedbackOutput | null>(null);
  protected reply = '';
  protected replyStatus = FeedbackStatus.Answered;

  protected readonly Kind = FeedbackKind;
  protected readonly Status = FeedbackStatus;

  protected readonly statuses = [
    { value: null, key: 'common.all' },
    { value: FeedbackStatus.New, key: 'feedback.status.new' },
    { value: FeedbackStatus.InProgress, key: 'feedback.status.inProgress' },
    { value: FeedbackStatus.Answered, key: 'feedback.status.answered' },
    { value: FeedbackStatus.Closed, key: 'feedback.status.closed' },
  ];

  private static readonly KIND_KEYS: Record<FeedbackKind, string> = {
    [FeedbackKind.Suggestion]: 'feedback.kind.suggestion',
    [FeedbackKind.Complaint]: 'feedback.kind.complaint',
    [FeedbackKind.Correction]: 'feedback.kind.correction',
    [FeedbackKind.Praise]: 'feedback.kind.praise',
  };

  private static readonly STATUS_KEYS: Record<FeedbackStatus, string> = {
    [FeedbackStatus.New]: 'feedback.status.new',
    [FeedbackStatus.InProgress]: 'feedback.status.inProgress',
    [FeedbackStatus.Answered]: 'feedback.status.answered',
    [FeedbackStatus.Closed]: 'feedback.status.closed',
  };

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);

    this.api.feedback(this.filter(), { pageSize: 50 }).subscribe((response) => {
      this.rows.set(response.data?.data ?? []);
      this.loading.set(false);
    });
  }

  protected filterBy(status: FeedbackStatus | null): void {
    this.filter.set(status);
    this.load();
  }

  protected openReply(row: FeedbackOutput): void {
    this.replyingTo.set(row);
    this.reply = row.reply ?? '';
    this.replyStatus = FeedbackStatus.Answered;
  }

  protected send(): void {
    const row = this.replyingTo();
    if (!row || !this.reply.trim() || this.saving()) return;

    this.saving.set(true);
    this.error.set(null);

    this.api
      .replyToFeedback(row.id, this.reply.trim(), this.replyStatus)
      .subscribe((response) => {
        this.saving.set(false);

        if (!response.success) {
          this.error.set(errorKey(response.errorCode));
          return;
        }

        this.replyingTo.set(null);
        this.load();
      });
  }

  protected close(): void {
    this.replyingTo.set(null);
    this.error.set(null);
  }

  protected kindKey(kind: FeedbackKind): string {
    return FeedbackComponent.KIND_KEYS[kind];
  }

  protected statusKey(status: FeedbackStatus): string {
    return FeedbackComponent.STATUS_KEYS[status];
  }
}
