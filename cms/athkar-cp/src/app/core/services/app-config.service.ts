import { DOCUMENT } from '@angular/common';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { AppConfigurationOutput } from '../api/models';
import { ApiService } from '../api/api.service';
import { applyBrandColor } from './brand-color';

/**
 * Fetches the platform configuration and paints the brand before first render.
 *
 * Registered through `provideAppInitializer`, and it **never rejects**: a
 * console that will not start because a colour could not be fetched is a worse
 * failure than one that starts in the default green.
 */
@Injectable({ providedIn: 'root' })
export class AppConfigService {
  private readonly api = inject(ApiService);
  private readonly document = inject(DOCUMENT);

  readonly configuration = signal<AppConfigurationOutput | null>(null);

  async load(): Promise<void> {
    try {
      const response = await firstValueFrom(this.api.publicConfiguration());

      if (response.success && response.data) {
        this.configuration.set(response.data);
        applyBrandColor(response.data.primaryColor, this.document);
      }
    } catch {
      // Deliberately silent. The stylesheet's own default is already correct.
    }
  }

  /** Called after the settings screen saves, so the change is visible at once. */
  apply(configuration: AppConfigurationOutput): void {
    this.configuration.set(configuration);
    applyBrandColor(configuration.primaryColor, this.document);
  }
}
