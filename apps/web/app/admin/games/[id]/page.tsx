'use client';
import { useEffect, useState } from 'react';
import { useParams } from 'next/navigation';
import Link from 'next/link';
import { useTranslations } from 'next-intl';
import { apiFetch } from '../../../../lib/api-client';
import { DataTable } from '../../../../components/admin/DataTable';
import { ProductFormModal, ProductFormData } from '../../../../components/admin/games/ProductFormModal';

interface Product {
  id: string;
  name: string;
  sku: string;
  priceCost: number;
  priceSell: number;
  currency: string;
  productType: 'DIRECT' | 'VOUCHER';
  requiresServer: boolean;
  requiresUsername: boolean;
  isActive: boolean;
  sortOrder: number;
}

interface Game {
  id: string;
  name: string;
  slug: string;
}

export default function GameDetailPage() {
  const t = useTranslations('admin.products');
  const tc = useTranslations('common');
  const { id } = useParams<{ id: string }>();
  const [game, setGame] = useState<Game | null>(null);
  const [products, setProducts] = useState<Product[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [modal, setModal] = useState<{ mode: 'create' } | { mode: 'edit'; product: Product } | null>(null);

  const loadGame = async () => {
    try {
      const all = await apiFetch<Game[]>('/admin/games');
      setGame(all.find((g) => g.id === id) ?? null);
    } catch (e: any) {
      setError(e.message);
    }
  };

  const loadProducts = async () => {
    try {
      const data = await apiFetch<Product[]>(`/admin/games/${id}/products`);
      setProducts(data);
    } catch (e: any) {
      setError(e.message);
    }
  };

  useEffect(() => {
    loadGame();
    loadProducts();
  }, [id]); // eslint-disable-line react-hooks/exhaustive-deps

  const handleCreate = async (data: ProductFormData) => {
    await apiFetch(`/admin/games/${id}/products`, { method: 'POST', body: JSON.stringify(data) });
    setModal(null);
    loadProducts();
  };

  const handleEdit = async (data: ProductFormData) => {
    if (modal?.mode !== 'edit') return;
    await apiFetch(`/admin/games/${id}/products/${modal.product.id}`, { method: 'PATCH', body: JSON.stringify(data) });
    setModal(null);
    loadProducts();
  };

  const handleDelete = async (product: Product) => {
    if (!confirm(t('deleteConfirm', { name: product.name }))) return;
    try {
      await apiFetch(`/admin/games/${id}/products/${product.id}`, { method: 'DELETE' });
      loadProducts();
    } catch (e: any) {
      setError(e.message);
    }
  };

  if (error) return <div className="text-pink">{tc('error', { message: error })}</div>;

  return (
    <div className="max-w-5xl mx-auto">
      <div className="flex items-center gap-3 mb-6">
        <Link href="/admin/games" className="text-frost/40 hover:text-frost text-sm">{t('backToGames')}</Link>
        <h1 className="font-display text-2xl text-frost">{t('title', { name: game?.name ?? t('fallbackGame') })}</h1>
      </div>

      <div className="flex justify-end mb-4">
        <button
          onClick={() => setModal({ mode: 'create' })}
          className="grad-brand text-white px-4 py-2 pixel-cut text-sm font-bold hover:brightness-110 transition-colors"
        >
          {t('addProduct')}
        </button>
      </div>

      <DataTable<Product>
        emptyMessage={t('noProducts')}
        data={products}
        columns={[
          { key: 'name', header: t('name'), render: (_, p) => <span className="font-medium text-frost">{p.name}</span> },
          { key: 'sku', header: 'SKU', render: (_, p) => <span className="font-mono text-frost/50">{p.sku}</span> },
          { key: 'priceCost', header: t('cost'), align: 'right', render: (_, p) => <span className="font-mono tabular-nums text-frost/50">฿{Number(p.priceCost).toFixed(2)}</span> },
          { key: 'priceSell', header: t('sell'), align: 'right', render: (_, p) => <span className="font-mono tabular-nums font-semibold text-frost">฿{Number(p.priceSell).toFixed(2)}</span> },
          {
            key: 'productType',
            header: t('type'),
            align: 'center',
            render: (_, p) => <span className="text-xs bg-neon/15 text-neon px-2 py-0.5 rounded-full">{p.productType}</span>,
          },
          {
            key: 'isActive',
            header: t('status'),
            align: 'center',
            render: (_, p) => (
              <span className={`px-2 py-0.5 rounded-full text-xs font-medium ${p.isActive ? 'bg-mint/15 text-mint' : 'bg-frost/10 text-frost/50'}`}>
                {p.isActive ? tc('active') : tc('inactive')}
              </span>
            ),
          },
          {
            key: 'id',
            header: '',
            render: (_, p) => (
              <div className="flex items-center justify-end gap-3">
                <button onClick={() => setModal({ mode: 'edit', product: p })} className="text-frost/50 hover:text-frost text-xs">{tc('edit')}</button>
                <button onClick={() => handleDelete(p)} className="text-pink hover:text-pink-dim text-xs">{tc('delete')}</button>
              </div>
            ),
          },
        ]}
      />

      {modal?.mode === 'create' && (
        <ProductFormModal title={t('addProductTitle')} onSubmit={handleCreate} onClose={() => setModal(null)} />
      )}
      {modal?.mode === 'edit' && (
        <ProductFormModal
          title={t('editTitle', { name: modal.product.name })}
          initial={{
            ...modal.product,
            priceCost: Number(modal.product.priceCost),
            priceSell: Number(modal.product.priceSell),
          }}
          onSubmit={handleEdit}
          onClose={() => setModal(null)}
        />
      )}
    </div>
  );
}
