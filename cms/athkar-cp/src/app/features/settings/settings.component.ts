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
    { value: CalculationMethod.UmmAlQura, label: 'أم القرى' },
    { value: CalculationMethod.MuslimWorldLeague, label: 'رابطة العالم الإسلامي' },
    { value: CalculationMethod.Egyptian, label: 'الهيئة المصرية' },
    { value: CalculationMethod.Karachi, label: 'كراتشي' },
    { value: CalculationMethod.Kuwait, label: 'الكويت' },
    { value: CalculationMethod.Qatar, label: 'قطر' },
    { value: CalculationMethod.Dubai, label: 'الإمارات' },
    { value: CalculationMethod.Turkey, label: 'ديانت' },
    { value: CalculationMethod.NorthAmerica, label: 'ISNA' },
    { value: CalculationMethod.Singapore, label: 'سنغافورة' },
    { value: CalculationMethod.Tehran, label: 'طهران' },
    { value: CalculationMethod.MoonsightingCommittee, label: 'لجنة رؤية الهلال' },
    { value: CalculationMethod.Jordan, label: 'دائرة الإفتاء الأردنية' },
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
