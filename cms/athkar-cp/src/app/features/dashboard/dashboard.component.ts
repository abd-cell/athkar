import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { ApiService } from '../../core/api/api.service';
import { DashboardOutput } from '../../core/api/models';
import { TranslatePipe } from '../../core/pipes/translate.pipe';
import { GlobalService } from '../../core/services/global.service';

@Component({
  selector: 'app-dashboard',
  imports: [TranslatePipe, RouterLink],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
})
export class DashboardComponent {
  private readonly api = inject(ApiService);
  protected readonly global = inject(GlobalService);

  protected readonly data = signal<DashboardOutput | null>(null);
  protected readonly loading = signal(true);

  constructor() {
    this.api.dashboard().subscribe((response) => {
      this.data.set(response.data ?? null);
      this.loading.set(false);
    });
  }

  /** The installs series, as points on a 0–100 box for the sparkline. */
  protected points(): string {
    const installs = this.data()?.installs ?? [];
    if (installs.length === 0) return '';

    const peak = Math.max(1, ...installs.map((day) => day.count));

    return installs
      .map((day, index) => {
        const x = (index / Math.max(1, installs.length - 1)) * 100;
        const y = 100 - (day.count / peak) * 100;
        return `${x.toFixed(2)},${y.toFixed(2)}`;
      })
      .join(' ');
  }

  protected entries(map: Record<string, number> | undefined): [string, number][] {
    return Object.entries(map ?? {}).sort((a, b) => b[1] - a[1]);
  }

  protected share(value: number, of: Record<string, number> | undefined): number {
    const total = Object.values(of ?? {}).reduce((sum, count) => sum + count, 0);
    return total === 0 ? 0 : Math.round((value / total) * 100);
  }
}
