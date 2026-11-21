'use client';
import { useEffect, useState } from 'react';
import { useTranslations } from 'next-intl';
import { apiFetch } from '../../../lib/api-client';
import { DataTable } from '../../../components/admin/DataTable';
import { ProviderFormModal, ProviderFormData } from '../../../components/admin/providers/ProviderFormModal';

interface Provider {
  id: string;
  name: string;
  slug: string;
  apiUrl: string;
  priority: number;
  isActive: boolean;
  rateLimitPerMin: number;
  healthCheckUrl: string | null;
}

export default function AdminProvidersPage() {
  const t = useTranslations('admin.providers');
  const tc = useTranslations('common');
  const [providers, setProviders] = useState<Provider[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [modal, setModal] = useState<{ mode: 'create' } | { mode: 'edit'; provider: Provider } | null>(null);

  const load = async () => {
    try {
      const data = await apiFetch<Provider[]>('/admin/providers');
      setProviders(data);
    } catch (e: any) {
      setError(e.message);
    }
  };

  useEffect(() => { load(); }, []); // eslint-disable-line react-hooks/exhaustive-deps

  const handleCreate = async (data: ProviderFormData) => {
    await apiFetch('/admin/providers', { method: 'POST', body: JSON.stringify(data) });
    setModal(null);
    load();
  };

  const handleEdit = async (data: ProviderFormData) => {
    if (modal?.mode !== 'edit') return;
    await apiFetch(`/admin/providers/${modal.provider.id}`, { method: 'PATCH', body: JSON.stringify(data) });
    setModal(null);
    load();
  };

  if (error) return <div className="text-pink">{tc('error', { message: error })}</div>;

  return (
    <div className="max-w-3xl mx-auto">
      <div className="flex items-center justify-between mb-6">
        <h1 className="font-display text-2xl text-frost">{t('title')}</h1>
        <button
          onClick={() => setModal({ mode: 'create' })}
          className="grad-brand text-white px-4 py-2 pixel-cut text-sm font-bold hover:brightness-110 transition-colors"
        >
          {t('newProvider')}
        </button>
      </div>

      <p className="text-sm text-frost/50 mb-4">{t('credentialHint')}</p>

      <DataTable<Provider>
        emptyMessage={t('noProviders')}
        data={providers}
        columns={[
          { key: 'name', header: t('name'), render: (_, p) => <span className="font-medium text-frost">{p.name}</span> },
          { key: 'slug', header: t('slug'), render: (_, p) => <span className="text-frost/50 font-mono">{p.slug}</span> },
          { key: 'priority', header: t('priority'), align: 'center', render: (_, p) => <span className="text-frost/50 font-mono tabular-nums">{p.priority}</span> },
          { key: 'rateLimitPerMin', header: t('rateLimit'), align: 'center', render: (_, p) => <span className="text-frost/50 font-mono tabular-nums">{p.rateLimitPerMin}/min</span> },
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
              <div className="flex justify-end">
                <button onClick={() => setModal({ mode: 'edit', provider: p })} className="text-frost/50 hover:text-frost text-xs">{tc('edit')}</button>
              </div>
            ),
          },
        ]}
      />

      {modal?.mode === 'create' && (
        <ProviderFormModal title={t('newProviderTitle')} isEdit={false} onSubmit={handleCreate} onClose={() => setModal(null)} />
      )}
      {modal?.mode === 'edit' && (
        <ProviderFormModal
          title={t('editTitle', { name: modal.provider.name })}
          isEdit
          initial={{ ...modal.provider, healthCheckUrl: modal.provider.healthCheckUrl ?? '', apiKey: '', apiSecret: '' } as Partial<ProviderFormData>}
          onSubmit={handleEdit}
          onClose={() => setModal(null)}
        />
      )}
    </div>
  );
}
