import Image from 'next/image';
import Link from 'next/link';
import { getTranslations } from 'next-intl/server';
import { LanguageSwitcher } from '../../components/ui/LanguageSwitcher';
import { ThemeToggle } from '../../components/theme/ThemeToggle';

const linkClass = 'shrink-0 py-1 text-sm font-body text-frost/70 hover:text-pixel-bright transition-colors whitespace-nowrap';

export default async function AdminLayout({ children }: { children: React.ReactNode }) {
  const t = await getTranslations('admin.nav');
  const links = [
    { href: '/admin', label: t('dashboard') },
    { href: '/admin/games', label: t('games') },
    { href: '/admin/categories', label: t('categories') },
    { href: '/admin/providers', label: t('providers') },
    { href: '/admin/orders', label: t('orders') },
    { href: '/admin/users', label: t('users') },
  ];

  return (
    <div className="min-h-screen bg-void">
      {/* lg and up: one row. Below: brand + controls on top, the section links in a sideways-scrolling row. */}
      <nav className="bg-void-deep text-frost border-b border-frost/10">
        <div className="px-4 sm:px-6 py-3 flex flex-wrap items-center gap-x-3 sm:gap-x-6 gap-y-2">
          <span className="flex items-center gap-2 shrink-0">
            <Image src="/logo-mark.png" alt="" width={32} height={26} className="mix-blend-screen" />
            <span className="font-display italic font-bold text-lg whitespace-nowrap">
              <span className="text-frost">Pixel</span><span className="grad-text">Pay</span>
              <span className="hidden sm:inline text-frost/50 text-sm font-body not-italic font-normal align-middle ml-2">{t('badge')}</span>
            </span>
          </span>
          <div className="ml-auto flex items-center gap-3 sm:gap-4 lg:order-3">
            <ThemeToggle />
            <LanguageSwitcher />
            <Link href="/" className="text-sm font-body text-frost/40 hover:text-pixel-bright transition-colors whitespace-nowrap">
              {t('backToStore')}
            </Link>
          </div>
          <div className="w-full lg:w-auto lg:order-2 -mx-4 px-4 sm:-mx-6 sm:px-6 lg:mx-0 lg:px-0 flex items-center gap-5 overflow-x-auto no-scrollbar">
            {links.map((l) => (
              <Link key={l.href} href={l.href} className={linkClass}>{l.label}</Link>
            ))}
          </div>
        </div>
      </nav>
      <main className="p-4 sm:p-6">{children}</main>
    </div>
  );
}
