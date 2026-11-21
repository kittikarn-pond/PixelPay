import { SiteHeader } from '../../components/layout/SiteHeader';

// Same header as the store (brand, glow strip, phone menu), with the admin sections.
export default function AdminLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="min-h-screen bg-void">
      <SiteHeader variant="admin" />
      <main className="p-4 sm:p-6">{children}</main>
    </div>
  );
}
