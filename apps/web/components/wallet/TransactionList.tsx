'use client';
import { useTranslations } from 'next-intl';

export interface WalletTransaction {
  id: string;
  type:
    | 'DEPOSIT'
    | 'WITHDRAWAL'
    | 'TOPUP_DEBIT'
    | 'TOPUP_REFUND'
    | 'CASHBACK_CREDIT'
    | 'AFFILIATE_CREDIT'
    | 'LOCK'
    | 'UNLOCK';
  amount: string;
  balanceAfter: string;
  metadata: { description?: string } | null;
  createdAt: string;
}

const TYPE_CONFIG: Record<WalletTransaction['type'], { key: string; credit: boolean }> = {
  DEPOSIT: { key: 'deposit', credit: true },
  WITHDRAWAL: { key: 'withdrawal', credit: false },
  TOPUP_DEBIT: { key: 'topup', credit: false },
  TOPUP_REFUND: { key: 'refund', credit: true },
  CASHBACK_CREDIT: { key: 'cashback', credit: true },
  AFFILIATE_CREDIT: { key: 'affiliate', credit: true },
  LOCK: { key: 'hold', credit: false },
  UNLOCK: { key: 'holdReleased', credit: true },
};

const baht = (v: string) => `฿${Number(v).toLocaleString('th-TH', { minimumFractionDigits: 2 })}`;

export function TransactionList({ transactions }: { transactions: WalletTransaction[] }) {
  const t = useTranslations('wallet');
  const describe = (tx: WalletTransaction) => {
    const config = TYPE_CONFIG[tx.type];
    return { label: config ? t(`types.${config.key}`) : tx.type, credit: config?.credit ?? false };
  };

  return (
    <>
      {/* Phones: one card per transaction. */}
      <div className="md:hidden pixel-cut bg-panel border border-frost/10 divide-y divide-frost/10 text-sm">
        {transactions.length === 0 && <p className="text-center py-12 text-frost/40">{t('noTransactions')}</p>}
        {transactions.map((tx) => {
          const { label, credit } = describe(tx);
          return (
            <div key={tx.id} className="px-4 py-3">
              <div className="flex items-start justify-between gap-4">
                <div className="min-w-0">
                  <p className="text-frost font-medium">{label}</p>
                  <p className="text-frost/50 break-words">{tx.metadata?.description ?? '—'}</p>
                </div>
                <p className={`shrink-0 font-mono tabular-nums font-medium ${credit ? 'text-mint' : 'text-pink'}`}>
                  {credit ? '+' : '−'}{baht(tx.amount)}
                </p>
              </div>
              <div className="mt-1 flex items-center justify-between gap-4 text-xs text-frost/40">
                <span>{new Date(tx.createdAt).toLocaleString('th-TH')}</span>
                <span className="font-mono tabular-nums">{t('table.balance')} {baht(tx.balanceAfter)}</span>
              </div>
            </div>
          );
        })}
      </div>

    <div className="hidden md:block pixel-cut bg-panel border border-frost/10 overflow-x-auto">
      <table className="w-full text-sm">
        <thead className="bg-void-deep">
          <tr>
            <th className="text-left px-4 py-3 font-mono text-[10px] uppercase tracking-wider text-frost/60">{t('table.date')}</th>
            <th className="text-left px-4 py-3 font-mono text-[10px] uppercase tracking-wider text-frost/60">{t('table.type')}</th>
            <th className="text-left px-4 py-3 font-mono text-[10px] uppercase tracking-wider text-frost/60">{t('table.description')}</th>
            <th className="text-right px-4 py-3 font-mono text-[10px] uppercase tracking-wider text-frost/60">{t('table.amount')}</th>
            <th className="text-right px-4 py-3 font-mono text-[10px] uppercase tracking-wider text-frost/60">{t('table.balance')}</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-frost/5">
          {transactions.length === 0 && (
            <tr>
              <td colSpan={5} className="text-center py-12 text-frost/40">{t('noTransactions')}</td>
            </tr>
          )}
          {transactions.map((tx) => {
            const { label, credit } = describe(tx);
            return (
              <tr key={tx.id} className="hover:bg-panel-light/60">
                <td className="px-4 py-3 text-frost/50 whitespace-nowrap">
                  {new Date(tx.createdAt).toLocaleString('th-TH')}
                </td>
                <td className="px-4 py-3 text-frost">{label}</td>
                <td className="px-4 py-3 text-frost/50">{tx.metadata?.description ?? '—'}</td>
                <td
                  className={`px-4 py-3 text-right font-mono tabular-nums font-medium whitespace-nowrap ${credit ? 'text-mint' : 'text-pink'}`}
                >
                  {credit ? '+' : '−'}{baht(tx.amount)}
                </td>
                <td className="px-4 py-3 text-right font-mono tabular-nums text-frost/60 whitespace-nowrap">
                  {baht(tx.balanceAfter)}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
    </>
  );
}
