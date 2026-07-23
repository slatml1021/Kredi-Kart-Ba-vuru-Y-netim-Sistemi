import { Component, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app-shell.html',
  styleUrl: './app-shell.scss',
})
export class AppShell {
  protected readonly mode = signal<'officer' | 'manager'>('officer');

  constructor(
    protected readonly authService: AuthService,
    private readonly router: Router,
    route: ActivatedRoute,
  ) {
    this.mode.set(route.snapshot.data['mode'] as 'officer' | 'manager');
  }

  protected logout(): void {
    this.authService.logout();
    void this.router.navigate(['/login']);
  }
}
