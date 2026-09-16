import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { ApiService, errorKey } from '../../core/api/api.service';
import { Roles, StaffInput, StaffOutput } from '../../core/api/models';
import { AppDatePipe } from '../../core/pipes/app-date.pipe';
import { TranslatePipe } from '../../core/pipes/translate.pipe';

@Component({
  selector: 'app-staff',
  imports: [FormsModule, AppDatePipe, TranslatePipe],
  templateUrl: './staff.component.html',
})
export class StaffComponent {
  private readonly api = inject(ApiService);

  protected readonly rows = signal<StaffOutput[]>([]);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly editing = signal<StaffInput | null>(null);
  protected readonly editingId = signal<number | null>(null);

  protected readonly allRoles = [
    { value: Roles.Editor, key: 'staff.role.editor' },
    { value: Roles.Admin, key: 'staff.role.admin' },
    { value: Roles.SuperAdmin, key: 'staff.role.superAdmin' },
  ];

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);

    this.api.staff({ pageSize: 100 }).subscribe((response) => {
      this.rows.set(response.data?.data ?? []);
      this.loading.set(false);
    });
  }

  protected create(): void {
    this.editingId.set(null);
    this.editing.set({
      email: '',
      fullName: '',
      password: '',
      languageCode: 'ar',
      isActive: true,
      roles: [Roles.Editor],
    });
  }

  protected edit(row: StaffOutput): void {
    this.editingId.set(row.id);
    this.editing.set({
      email: row.email,
      fullName: row.fullName,
      // Empty means "keep the current one" — the server treats a null password
      // as no change rather than as a blank one.
      password: '',
      languageCode: row.languageCode,
      isActive: row.isActive,
      roles: [...row.roles],
    });
  }

  protected toggleRole(role: Roles): void {
    const form = this.editing();
    if (!form) return;

    form.roles = form.roles.includes(role)
      ? form.roles.filter((held) => held !== role)
      : [...form.roles, role];
  }

  protected hasRole(roles: Roles[], role: Roles): boolean {
    return roles.includes(role);
  }

  protected save(): void {
    const input = this.editing();
    if (!input || this.saving()) return;

    this.saving.set(true);
    this.error.set(null);

    const id = this.editingId();
    const payload = { ...input, password: input.password?.trim() ? input.password : null };
    const request = id === null ? this.api.createStaff(payload) : this.api.updateStaff(id, payload);

    request.subscribe((response) => {
      this.saving.set(false);

      if (!response.success) {
        this.error.set(errorKey(response.errorCode));
        return;
      }

      this.editing.set(null);
      this.load();
    });
  }

  protected remove(row: StaffOutput): void {
    if (!confirm(row.email)) return;

    this.api.deleteStaff(row.id).subscribe((response) => {
      if (response.success) this.load();
      else this.error.set(errorKey(response.errorCode));
    });
  }

  protected cancel(): void {
    this.editing.set(null);
    this.error.set(null);
  }

  protected roleKey(role: Roles): string {
    return this.allRoles.find((option) => option.value === role)?.key ?? '';
  }
}
