'use client';
import { useEffect, useState } from 'react';
import Link from 'next/link';
import { useTranslations } from 'next-intl';
import { apiFetch } from '../../../lib/api-client';
import { DataTable } from '../../../components/admin/DataTable';
import { GameFormModal, GameFormData } from '../../../components/admin/games/GameFormModal';

interface Category {
  id: string;
  name: string;
  slug: string;
}

interface Game {
  id: string;
  name: string;
  slug: string;
  category: Category | null;
  description: string | null;
  descriptionTh: string | null;
  isActive: boolean;
  sortOrder: number;
  logoUrl: string | null;
}

export default function AdminGamesPage() {
  const t = useTranslations('admin.games');
  const tc = useTranslations('common');
  const [games, setGames] = useState<Game[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [modal, setModal] = useState<{ mode: 'create' } | { mode: 'edit'; game: Game } | null>(null);

  const load = async () => {
    try {
      const data = await apiFetch<Game[]>('/admin/games');
      setGames(data);
    } catch (e: any) {
      setError(e.message);
    }
  };

  useEffect(() => { load(); }, []); // eslint-disable-line react-hooks/exhaustive-deps

  const handleCreate = async (data: GameFormData) => {
    await apiFetch('/admin/games', { method: 'POST', body: JSON.stringify({ ...data, categoryId: data.categoryId || null }) });
    setModal(null);
    load();
  };

  const handleEdit = async (data: GameFormData) => {
    if (modal?.mode !== 'edit') return;
    await apiFetch(`/admin/games/${modal.game.id}`, { method: 'PATCH', body: JSON.stringify({ ...data, categoryId: data.categoryId || null }) });
    setModal(null);
    load();
  };

  const handleDelete = async (game: Game) => {
    if (!confirm(t('deleteConfirm', { name: game.name }))) return;
    try {
      await apiFetch(`/admin/games/${game.id}`, { method: 'DELETE' });
      load();
    } catch (e: any) {
      setError(e.message);
    }
  };

  if (error) return <div className="text-pink">{tc('error', { message: error })}</div>;

  return (
    <div className="max-w-5xl mx-auto">
      <div className="flex items-center justify-between mb-6">
        <h1 className="font-display text-2xl text-frost">{t('title')}</h1>
        <button
          onClick={() => setModal({ mode: 'create' })}
          className="grad-brand text-white px-4 py-2 pixel-cut text-sm font-bold hover:brightness-110 transition-colors"
        >
          {t('newGame')}
        </button>
      </div>

      <DataTable<Game>
        emptyMessage={t('noGames')}
        data={games}
        columns={[
          { key: 'name', header: t('name'), render: (_, g) => <span className="font-medium text-frost">{g.name}</span> },
          { key: 'slug', header: t('slug'), render: (_, g) => <span className="text-frost/50 font-mono">{g.slug}</span> },
          { key: 'category', header: t('category'), render: (_, g) => <span className="text-frost/50">{g.category?.name ?? '—'}</span> },
          { key: 'sortOrder', header: t('order'), align: 'center', render: (_, g) => <span className="text-frost/50 font-mono tabular-nums">{g.sortOrder}</span> },
          {
            key: 'isActive',
            header: t('status'),
            align: 'center',
            render: (_, g) => (
              <span className={`px-2 py-0.5 rounded-full text-xs font-medium ${g.isActive ? 'bg-mint/15 text-mint' : 'bg-frost/10 text-frost/50'}`}>
                {g.isActive ? tc('active') : tc('inactive')}
              </span>
            ),
          },
          {
            key: 'id',
            header: '',
            render: (_, g) => (
              <div className="flex items-center justify-end gap-3">
                <Link href={`/admin/games/${g.id}`} className="text-neon hover:underline text-xs font-semibold">{t('products')}</Link>
                <button onClick={() => setModal({ mode: 'edit', game: g })} className="text-frost/50 hover:text-frost text-xs">{tc('edit')}</button>
                <button onClick={() => handleDelete(g)} className="text-pink hover:text-pink-dim text-xs">{tc('delete')}</button>
              </div>
            ),
          },
        ]}
      />

      {modal?.mode === 'create' && (
        <GameFormModal title={t('newGameTitle')} onSubmit={handleCreate} onClose={() => setModal(null)} />
      )}
      {modal?.mode === 'edit' && (
        <GameFormModal
          title={t('editTitle', { name: modal.game.name })}
          initial={{
            ...modal.game,
            categoryId: modal.game.category?.id,
            logoUrl: modal.game.logoUrl ?? undefined,
            description: modal.game.description ?? undefined,
            descriptionTh: modal.game.descriptionTh ?? undefined,
          }}
          onSubmit={handleEdit}
          onClose={() => setModal(null)}
        />
      )}
    </div>
  );
}
