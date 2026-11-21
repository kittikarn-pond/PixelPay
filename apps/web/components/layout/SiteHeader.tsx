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

const linkClass = 'text-sm font-body text-frost/70 hover:text-pixel-bright transition-colors whitespace-nowrap';
const menuLinkClass = 'block px-4 py-3 text-base font-body text-frost/80 hover:bg-panel-light/60 hover:text-pixel-bright transition-colors';

export function SiteHeader() {
  const t = useTranslations('nav');
  const router = useRouter();
  const pathname = usePathname();
  const user = useAuthStore((s) => s.user);
  const logout = useAuthStore((s) => s.logout);
  const [wallet, setWallet] = useState<Wallet | null>(null);
  const [menuOpen, setMenuOpen] = useState(false);

  useEffect(() => {
    if (!user) { setWallet(null); return; }
    apiFetch<Wallet>('/wallet').then(setWallet).catch(() => setWallet(null));
  }, [user]);

  // Close the phone menu whenever the route changes.
  useEffect(() => { setMenuOpen(false); }, [pathname]);

  const handleLogout = () => {
    document.cookie = 'pixelpay-token=; path=/; max-age=0';
    document.cookie = 'pixelpay-refresh=; path=/; max-age=0';
    setMenuOpen(false);
    logout();
    router.push('/login');
  };

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

  const navLinks = user
    ? [
        { href: '/orders', label: t('orders') },
        { href: '/wallet', label: t('wallet') },
        { href: '/affiliate', label: t('affiliate') },
        ...(user.role === 'ADMIN' ? [{ href: '/admin', label: t('admin'), admin: true }] : []),
      ]
    : [];

  return (
    <header className="bg-void-deep text-frost sticky top-0 z-40 border-b border-frost/10">
      <div className="glow-strip" />
      <div className="max-w-6xl mx-auto px-4 py-3 flex items-center justify-between gap-3">
        <Link href="/" className="flex items-center gap-2 shrink-0">
          <Image src="/logo-mark.png" alt="" width={40} height={33} priority className="mix-blend-screen w-8 h-auto sm:w-10" />
          <span className="font-display italic font-bold text-lg sm:text-xl tracking-wide">
            <span className="text-frost">Pixel</span><span className="grad-text">Pay</span>
          </span>
        </Link>

        {/* Desktop (md and up): everything inline. */}
        <div className="hidden md:flex items-center gap-4 min-w-0">
          <ThemeToggle />
          <LanguageSwitcher />
          {user ? (
            <>
              <nav className="flex items-center gap-4">
                {navLinks.map((l) => (
                  <Link key={l.href} href={l.href} className={l.admin ? `${linkClass} !text-neon` : linkClass}>
                    {l.label}
                  </Link>
                ))}
              </nav>
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

        {/* Phones and small tablets: the essentials, the rest behind the menu button. */}
        <div className="flex md:hidden items-center gap-2 min-w-0">
          {user ? credits : (
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
        <div id="site-menu" className="md:hidden border-t border-frost/10 bg-void-deep max-h-[calc(100dvh-4rem)] overflow-y-auto">
          <nav className="py-2">
            {navLinks.map((l) => (
              <Link key={l.href} href={l.href} className={l.admin ? `${menuLinkClass} !text-neon` : menuLinkClass}>
                {l.label}
              </Link>
            ))}
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
