'use client';
import { useEffect, useState } from 'react';
import Image from 'next/image';
import Link from 'next/link';
import { usePathname, useRouter } from 'next/navigation';
import { useTranslations } from 'next-intl';
import { Menu, X } from 'lucide-react';
import { apiFetch } from '../../lib/api-client';
import { useAuthStore } from '../../stores/auth.store';
import { CreditCounter } from '../ui/CreditCounter';
import { LanguageSwitcher } from '../ui/LanguageSwitcher';
import { ThemeToggle } from '../theme/ThemeToggle';

interface Wallet {
  balance: string;
}

interface NavLink {
  href: string;
  label: string;
  /** Highlighted in the accent colour (the Admin entry on the store side). */
  accent?: boolean;
  /** Active only on this exact path (e.g. /admin, which prefixes every admin page). */
  exact?: boolean;
}

// The breakpoint at which the inline nav replaces the menu button. Static strings so Tailwind keeps them.
const LAYOUT = {
  md: { inline: 'hidden md:flex', compact: 'flex md:hidden', drawer: 'md:hidden' },
  lg: { inline: 'hidden lg:flex', compact: 'flex lg:hidden', drawer: 'lg:hidden' },
} as const;

const linkClass = 'text-sm font-body text-frost/70 hover:text-pixel-bright transition-colors whitespace-nowrap';
const menuLinkClass = 'block px-4 py-3 text-base font-body text-frost/80 hover:bg-panel-light/60 hover:text-pixel-bright transition-colors';

/**
 * The one header for the whole app. `store` (default): the storefront and account pages. `admin`: the admin
 * area, with its section links and a way back to the store. Same brand, strip, spacing and phone menu in both.
 */
export function SiteHeader({ variant = 'store' }: { variant?: 'store' | 'admin' }) {
  const t = useTranslations('nav');
  const ta = useTranslations('admin.nav');
  const router = useRouter();
  const pathname = usePathname();
  const user = useAuthStore((s) => s.user);
  const logout = useAuthStore((s) => s.logout);
  const [wallet, setWallet] = useState<Wallet | null>(null);
  const [menuOpen, setMenuOpen] = useState(false);
  const admin = variant === 'admin';
  const layout = LAYOUT[admin ? 'lg' : 'md'];

  useEffect(() => {
    if (!user || admin) { setWallet(null); return; }
    apiFetch<Wallet>('/wallet').then(setWallet).catch(() => setWallet(null));
  }, [user, admin]);

  // Close the phone menu whenever the route changes.
  useEffect(() => { setMenuOpen(false); }, [pathname]);

  const handleLogout = () => {
    document.cookie = 'pixelpay-token=; path=/; max-age=0';
    document.cookie = 'pixelpay-refresh=; path=/; max-age=0';
    setMenuOpen(false);
    logout();
    router.push('/login');
  };

  const isActive = (l: NavLink) => (l.exact ? pathname === l.href : pathname === l.href || pathname.startsWith(`${l.href}/`));

  const navLinks: NavLink[] = admin
    ? [
        { href: '/admin', label: ta('dashboard'), exact: true },
        { href: '/admin/games', label: ta('games') },
        { href: '/admin/categories', label: ta('categories') },
        { href: '/admin/providers', label: ta('providers') },
        { href: '/admin/orders', label: ta('orders') },
        { href: '/admin/users', label: ta('users') },
      ]
    : user
      ? [
          { href: '/orders', label: t('orders') },
          { href: '/wallet', label: t('wallet') },
          { href: '/affiliate', label: t('affiliate') },
          ...(user.role === 'ADMIN' ? [{ href: '/admin', label: t('admin'), accent: true }] : []),
        ]
      : [];

  const inlineClass = (l: NavLink) =>
    `${linkClass} ${isActive(l) ? '!text-pixel-bright' : l.accent ? '!text-neon' : ''}`;
  const drawerClass = (l: NavLink) =>
    `${menuLinkClass} ${isActive(l) ? '!text-pixel-bright bg-panel-light/40' : l.accent ? '!text-neon' : ''}`;

  const credits = wallet && (
    <Link href="/wallet" className="shrink-0">
      <CreditCounter
        label={t('credits')}
        value={`฿${Number(wallet.balance).toLocaleString('th-TH', { minimumFractionDigits: 2 })}`}
        tone="neon"
        size="sm"
      />
    </Link>
  );

  return (
    <header className="bg-void-deep text-frost sticky top-0 z-40 border-b border-frost/10">
      <div className="glow-strip" />
      <div className="max-w-6xl mx-auto px-4 py-3 flex items-center justify-between gap-3">
        <Link href={admin ? '/admin' : '/'} className="flex items-center gap-2 shrink-0">
          <Image src="/logo-mark.png" alt="" width={40} height={33} priority className="mix-blend-screen w-8 h-auto sm:w-10" />
          <span className="font-display italic font-bold text-lg sm:text-xl tracking-wide whitespace-nowrap">
            <span className="text-frost">Pixel</span><span className="grad-text">Pay</span>
          </span>
          {admin && (
            <span className="font-mono text-[10px] uppercase tracking-wider text-neon border border-neon/40 px-1.5 py-0.5">
              {ta('badge')}
            </span>
          )}
        </Link>

        {/* Wide screens: everything inline. */}
        <div className={`${layout.inline} items-center gap-4 min-w-0`}>
          {navLinks.length > 0 && (
            <nav className="flex items-center gap-4">
              {navLinks.map((l) => (
                <Link key={l.href} href={l.href} className={inlineClass(l)} aria-current={isActive(l) ? 'page' : undefined}>
                  {l.label}
                </Link>
              ))}
            </nav>
          )}
          <ThemeToggle />
          <LanguageSwitcher />
          {admin && <Link href="/" className={linkClass}>{ta('backToStore')}</Link>}
          {user ? (
            <>
              {credits}
              <Link href="/profile" className="text-sm text-frost/60 font-body hover:text-pixel-bright transition-colors truncate max-w-[10rem]">
                {user.displayName}
              </Link>
              <button onClick={handleLogout} className={linkClass}>
                {t('logout')}
              </button>
            </>
          ) : (
            <>
              <Link href="/login" className={linkClass}>{t('login')}</Link>
              <Link
                href="/register"
                className="text-sm font-semibold font-body grad-brand text-white px-4 py-2 pixel-cut hover:brightness-110 transition-colors whitespace-nowrap"
              >
                {t('getCredits')}
              </Link>
            </>
          )}
        </div>

        {/* Phones and tablets: the essentials, the rest behind the menu button. */}
        <div className={`${layout.compact} items-center gap-2 min-w-0`}>
          {admin ? null : user ? credits : (
            <Link
              href="/register"
              className="text-sm font-semibold font-body grad-brand text-white px-3 py-2 pixel-cut hover:brightness-110 transition-colors whitespace-nowrap"
            >
              {t('getCredits')}
            </Link>
          )}
          <button
            type="button"
            onClick={() => setMenuOpen((o) => !o)}
            aria-expanded={menuOpen}
            aria-controls="site-menu"
            aria-label={menuOpen ? t('closeMenu') : t('menu')}
            className="p-2 -mr-2 text-frost/80 hover:text-pixel-bright transition-colors"
          >
            {menuOpen ? <X size={24} /> : <Menu size={24} />}
          </button>
        </div>
      </div>

      {menuOpen && (
        <div id="site-menu" className={`${layout.drawer} border-t border-frost/10 bg-void-deep max-h-[calc(100dvh-4rem)] overflow-y-auto`}>
          <nav className="py-2">
            {navLinks.map((l) => (
              <Link key={l.href} href={l.href} className={drawerClass(l)} aria-current={isActive(l) ? 'page' : undefined}>
                {l.label}
              </Link>
            ))}
            {admin && <Link href="/" className={menuLinkClass}>{ta('backToStore')}</Link>}
            {user ? (
              <>
                <Link href="/profile" className={menuLinkClass}>
                  {t('profile')}
                  <span className="block text-xs text-frost/40 truncate">{user.displayName}</span>
                </Link>
                <button onClick={handleLogout} className={`${menuLinkClass} w-full text-left`}>
                  {t('logout')}
                </button>
              </>
            ) : (
              <Link href="/login" className={menuLinkClass}>{t('login')}</Link>
            )}
          </nav>
          <div className="flex items-center justify-between gap-4 px-4 py-3 border-t border-frost/10">
            <span className="font-mono text-[10px] uppercase tracking-wider text-frost/50">{t('preferences')}</span>
            <div className="flex items-center gap-4">
              <ThemeToggle />
              <LanguageSwitcher />
            </div>
          </div>
        </div>
      )}
    </header>
  );
}
