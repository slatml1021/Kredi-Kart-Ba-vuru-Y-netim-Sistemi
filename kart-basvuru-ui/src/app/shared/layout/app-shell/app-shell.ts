import { Component, DestroyRef, HostListener, computed, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { AppNotification, PlatformApiService } from '../../../core/services/platform-api.service';
import { interval, startWith, switchMap } from 'rxjs';

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app-shell.html',
  styleUrl: './app-shell.scss',
})
export class AppShell {
  protected readonly mode = signal<'officer' | 'manager'>('officer');
  protected readonly sidebarOpen = signal(sessionStorage.getItem('sidebar-open') === 'true');
  protected readonly notifications = signal<AppNotification[]>([]);
  protected readonly notificationsOpen = signal(false);
  protected readonly notificationError = signal('');
  protected readonly isMarkingAll = signal(false);
  protected readonly unreadCount = computed(() => this.notifications().filter(x => !x.isRead).length);

  constructor(
    protected readonly authService: AuthService,
    private readonly router: Router,
    private readonly platformApi: PlatformApiService,
    route: ActivatedRoute,
    destroyRef: DestroyRef,
  ) {
    this.mode.set(route.snapshot.data['mode'] as 'officer' | 'manager');
    interval(30000).pipe(
      startWith(0),
      switchMap(() => this.platformApi.notifications()),
      takeUntilDestroyed(destroyRef),
    ).subscribe({ next: items => this.notifications.set(items) });
  }

  @HostListener('document:click')
  protected closeNotificationsFromOutside(): void { this.notificationsOpen.set(false); }

  @HostListener('document:keydown.escape')
  protected closeTransientPanels(): void {
    this.notificationsOpen.set(false);
    this.closeSidebar();
  }

  protected openNotification(item: AppNotification): void {
    this.notifications.update(items => items.map(x => x.id === item.id ? { ...x, isRead: true } : x));
    this.platformApi.markNotificationRead(item.id).subscribe({
      error: () => this.notificationError.set('Bildirim durumu kaydedilemedi.'),
    });
    this.notificationsOpen.set(false);
    if (item.link) void this.router.navigateByUrl(item.link);
  }

  protected notificationTime(value: string): string {
    return new Date(value).toLocaleString('tr-TR', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' });
  }

  protected markAllNotificationsRead(): void {
    if (!this.unreadCount() || this.isMarkingAll()) return;
    const previous = this.notifications();
    this.notificationError.set('');
    this.isMarkingAll.set(true);
    this.notifications.update(items => items.map(item => ({ ...item, isRead: true })));
    this.platformApi.markAllNotificationsRead().subscribe({
      next: () => this.isMarkingAll.set(false),
      error: () => {
        this.notifications.set(previous);
        this.isMarkingAll.set(false);
        this.notificationError.set('Bildirimler okundu olarak işaretlenemedi. Lütfen yeniden deneyin.');
      },
    });
  }

  protected logout(): void {
    this.authService.endSession().subscribe({
      next: () => this.finishLogout(),
      error: () => this.finishLogout(),
    });
  }

  private finishLogout(): void {
    this.authService.logout();
    void this.router.navigate(['/login'], { replaceUrl: true });
  }

  protected toggleSidebar(): void {
    this.sidebarOpen.update(value => !value);
    sessionStorage.setItem('sidebar-open', String(this.sidebarOpen()));
  }

  protected closeSidebar(): void {
    if (!this.sidebarOpen()) return;
    this.sidebarOpen.set(false);
    sessionStorage.setItem('sidebar-open', 'false');
  }

}
