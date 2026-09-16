import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { ApiService } from '../../core/api/api.service';
import { ApiLogOutput } from '../../core/api/models';
import { AppDatePipe } from '../../core/pipes/app-date.pipe';
import { TranslatePipe } from '../../core/pipes/translate.pipe';

/**
 * One row per `/api/` request, including the rejected ones — the middleware
 * runs ahead of authentication, which is what makes a 401 storm visible here.
 */
@Component({
  selector: 'app-logs',
  imports: [FormsModule, AppDatePipe, TranslatePipe],
  template: `
    <div class="page">
      <div class="page-head">
        <h1>{{ 'logs.title' | translate }}</h1>
        <span class="spacer"></span>
        <div class="row">
          @for (code of quickFilters; track code) {
            <button
              type="button"
              class="chip"
              [class.chip--on]="status() === code"
              (click)="filterBy(code)"
            >
              {{ code ?? ('common.all' | translate) }}
            </button>
          }
        </div>
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
                <th>{{ 'logs.method' | translate }}</th>
                <th>{{ 'logs.path' | translate }}</th>
                <th>{{ 'logs.status' | translate }}</th>
                <th>{{ 'logs.duration' | translate }}</th>
                <th>{{ 'logs.device' | translate }}</th>
              </tr>
            </thead>
            <tbody>
              @for (row of rows(); track row.id) {
                <tr>
                  <td>{{ row.createdAt | appDate }}</td>
                  <td><code>{{ row.method }}</code></td>
                  <td dir="ltr"><code style="font-size: 11.5px">{{ row.path }}</code></td>
                  <td>
                    <span
                      class="badge"
                      [class.badge--ok]="row.statusCode < 400"
                      [class.badge--warn]="row.statusCode >= 400 && row.statusCode < 500"
                      [class.badge--danger]="row.statusCode >= 500"
                    >
                      {{ row.statusCode }}
                      @if (row.errorCode) {
                        · {{ row.errorCode }}
                      }
                    </span>
                  </td>
                  <td>{{ row.durationMs }} ms</td>
                  <td dir="ltr">
                    <code style="font-size: 10.5px; color: var(--faint)">
                      {{ (row.deviceKey ?? '—').slice(0, 8) }}
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
export class LogsComponent {
  private readonly api = inject(ApiService);

  protected readonly rows = signal<ApiLogOutput[]>([]);
  protected readonly loading = signal(true);
  protected readonly status = signal<number | null>(null);

  /** The three answers worth filtering to in a hurry. */
  protected readonly quickFilters: (number | null)[] = [null, 401, 500];

  constructor() {
    this.load();
  }

  protected filterBy(code: number | null): void {
    this.status.set(code);
    this.load();
  }

  protected load(): void {
    this.loading.set(true);

    this.api.logs(this.status(), { pageSize: 100 }).subscribe((response) => {
      this.rows.set(response.data?.data ?? []);
      this.loading.set(false);
    });
  }
}
