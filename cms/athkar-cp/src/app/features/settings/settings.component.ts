import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { ApiService, errorKey } from '../../core/api/api.service';
import { AppConfigurationInput, CalculationMethod, Madhab } from '../../core/api/models';
import { TranslatePipe } from '../../core/pipes/translate.pipe';
import { AppConfigService } from '../../core/services/app-config.service';

/**
 * The single platform settings row.
 *
 * Saving re-applies the brand at once through [AppConfigService], so an admin
 * changing the colour sees the console change under them — which is the
 * cheapest possible preview of what the app will look like.
 */
@Component({
  selector: 'app-settings',
  imports: [FormsModule, TranslatePipe],
  templateUrl: './settings.component.html',
})
export class SettingsComponent {
  private readonly api = inject(ApiService);
  private readonly appConfig = inject(AppConfigService);

  protected readonly form = signal<AppConfigurationInput | null>(null);
  protected readonly contentVersion = signal(0);
  protected readonly saving = signal(false);
  protected readonly saved = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly methods = [
    { value: CalculationMethod.UmmAlQura, key: 'settings.method.ummAlQura' },
    { value: CalculationMethod.MuslimWorldLeague, key: 'settings.method.muslimWorldLeague' },
    { value: CalculationMethod.Egyptian, key: 'settings.method.egyptian' },
    { value: CalculationMethod.Karachi, key: 'settings.method.karachi' },
    { value: CalculationMethod.Kuwait, key: 'settings.method.kuwait' },
    { value: CalculationMethod.Qatar, key: 'settings.method.qatar' },
    { value: CalculationMethod.Dubai, key: 'settings.method.dubai' },
    { value: CalculationMethod.Turkey, key: 'settings.method.turkey' },
    { value: CalculationMethod.NorthAmerica, key: 'settings.method.northAmerica' },
    { value: CalculationMethod.Singapore, key: 'settings.method.singapore' },
    { value: CalculationMethod.Tehran, key: 'settings.method.tehran' },
    { value: CalculationMethod.MoonsightingCommittee, key: 'settings.method.moonsighting' },
    { value: CalculationMethod.Jordan, key: 'settings.method.jordan' },
  ];

  protected readonly Madhab = Madhab;

  constructor() {
    this.api.configuration().subscribe((response) => {
      const data = response.data;
      if (!data) return;

      this.contentVersion.set(data.contentVersion);

      const { contentVersion: _version, updatedAt: _updated, ...input } = data;
      this.form.set(input);
    });
  }

  protected save(): void {
    const input = this.form();
    if (!input || this.saving()) return;

    this.saving.set(true);
    this.saved.set(false);
    this.error.set(null);

    this.api.updateConfiguration(input).subscribe((response) => {
      this.saving.set(false);

      if (!response.success || !response.data) {
        this.error.set(errorKey(response.errorCode));
        return;
      }

      this.appConfig.apply(response.data);
      this.saved.set(true);
    });
  }
}
