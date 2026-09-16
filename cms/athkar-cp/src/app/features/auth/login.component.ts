import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';

import { ApiService, errorKey } from '../../core/api/api.service';
import { TranslatePipe } from '../../core/pipes/translate.pipe';
import { GlobalService } from '../../core/services/global.service';

@Component({
  selector: 'app-login',
  imports: [FormsModule, TranslatePipe],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent {
  private readonly api = inject(ApiService);
  private readonly global = inject(GlobalService);
  private readonly router = inject(Router);

  protected email = '';
  protected password = '';

  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected submit(): void {
    if (!this.email || !this.password || this.busy()) return;

    this.busy.set(true);
    this.error.set(null);

    this.api.login(this.email.trim(), this.password).subscribe((response) => {
      this.busy.set(false);

      if (!response.success || !response.data) {
        // 100 is InvalidCredentials and 102 AccountDisabled — the two a person
        // at this form can actually do something about. Everything else falls
        // through to the generic sentence.
        this.error.set(
          response.errorCode === 100
            ? 'login.failed'
            : response.errorCode === 102
              ? 'login.disabled'
              : errorKey(response.errorCode),
        );
        return;
      }

      this.global.signIn(response.data);
      void this.router.navigate(['/', this.global.languageCode(), 'dashboard']);
    });
  }
}
