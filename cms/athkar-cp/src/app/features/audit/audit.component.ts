import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { ApiService } from '../../core/api/api.service';
import { AuditOutput } from '../../core/api/models';
import { AppDatePipe } from '../../core/pipes/app-date.pipe';
import { TranslatePipe } from '../../core/pipes/translate.pipe';

/**
 * What staff have done.
 *
 * The action filter takes a prefix, so `content.` narrows to the whole content
 * slice without anybody having to know every action name in it.
 */
@Component({
  selector: 'app-audit',
  imports: [FormsModule, AppDatePipe, TranslatePipe],
  template: `
    <div class="page">
      <div class="page-head">
        <h1>{{ 'audit.title' | translate }}</h1>
        <span class="spacer"></span>
        <input
          type="text"
          dir="ltr"
          [(ngModel)]="action"
          (keyup.enter)="load()"
          placeholder="content."
          style="width: 200px"
        />
      </div>

      <div class="card card--flush">
        @if (loading()) {
          <div class="empty">{{ 'common.loading' | translate }}</div>
        } @else if (rows().length === 0) {
          <div class="empty">{{ 'common.empty' | translate }}</div>
        } @else {
          <table class="table">
            <thead>
              <tr>
                <th>{{ 'audit.when' | translate }}</th>
                <th>{{ 'audit.action' | translate }}</th>
                <th>{{ 'audit.entity' | translate }}</th>
                <th>{{ 'audit.user' | translate }}</th>
                <th>{{ 'audit.after' | translate }}</th>
              </tr>
            </thead>
            <tbody>
              @for (row of rows(); track row.id) {
                <tr>
                  <td>{{ row.createdAt | appDate }}</td>
                  <td><code>{{ row.action }}</code></td>
                  <td>{{ row.entityName }} @if (row.entityId) { #{{ row.entityId }} }</td>
                  <td>{{ row.userName ?? '—' }}</td>
                  <td>
                    <!--
                      The snapshot, truncated. It is JSON meant to be skimmed;
                      anything longer belongs in a copy-paste, not a column.
                    -->
                    <code style="font-size: 11px; color: var(--muted)">
                      {{ (row.newValue ?? '').slice(0, 90) }}
                    </code>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        }
      </div>
    </div>
  `,
})
export class AuditComponent {
  private readonly api = inject(ApiService);

  protected readonly rows = signal<AuditOutput[]>([]);
  protected readonly loading = signal(true);
  protected action = '';

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);

    this.api.audit(this.action.trim() || null, { pageSize: 100 }).subscribe((response) => {
      this.rows.set(response.data?.data ?? []);
      this.loading.set(false);
    });
  }
}
