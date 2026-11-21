'use client';
import { useEffect, useRef, useState } from 'react';
import { HttpTransportType, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { useTranslations } from 'next-intl';
import { Clock, Loader2, CheckCircle2, XCircle } from 'lucide-react';
import { getAccessToken } from '../../lib/api-client';
import { CreditCounter } from '../ui/CreditCounter';

type OrderStatus = 'PENDING' | 'PROCESSING' | 'COMPLETED' | 'FAILED';

const STATUS_CONFIG: Record<OrderStatus, { key: string; tone: 'gold' | 'neon' | 'mint' | 'pink'; Icon: typeof Clock }> = {
  PENDING: { key: 'pending', tone: 'gold', Icon: Clock },
  PROCESSING: { key: 'processing', tone: 'neon', Icon: Loader2 },
  COMPLETED: { key: 'completed', tone: 'mint', Icon: CheckCircle2 },
  FAILED: { key: 'failed', tone: 'pink', Icon: XCircle },
};

interface Props {
  orderId: string;
  initialStatus: OrderStatus;
  userId: string;
}

export function OrderTracker({ orderId, initialStatus, userId }: Props) {
  const t = useTranslations('orders');
  const [status, setStatus] = useState<OrderStatus>(initialStatus);
  const statusRef = useRef(status);

  // Keep ref in sync with state
  useEffect(() => {
    statusRef.current = status;
  }, [status]);

  useEffect(() => {
    if (statusRef.current === 'COMPLETED' || statusRef.current === 'FAILED') return;

    // SignalR wants an http(s) URL; accept the old ws(s):// form of NEXT_PUBLIC_WS_URL too.
    const base = (process.env.NEXT_PUBLIC_WS_URL ?? 'http://localhost:3000').replace(/^ws(s?):\/\//, 'http$1://');

    // The hub puts this connection in the caller's user group from the token; the server then sends
    // `order.status` for each of the user's orders.
    const connection = new HubConnectionBuilder()
      // WebSockets only, no negotiate round-trip: with several API replicas behind nginx, a negotiate and the
      // socket that follows could reach different replicas. The Redis backplane delivers across replicas.
      .withUrl(`${base}/hubs/orders`, {
        accessTokenFactory: () => getAccessToken() ?? '',
        skipNegotiation: true,
        transport: HttpTransportType.WebSockets,
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on('order.status', (data: { orderId: string; status: OrderStatus }) => {
      if (data.orderId === orderId) {
        setStatus(data.status);
        statusRef.current = data.status;
        if (data.status === 'COMPLETED' || data.status === 'FAILED') {
          void connection.stop();
        }
      }
    });

    connection.start().catch(() => {
      // Live updates are a nicety: the page still shows the status it loaded with.
    });

    return () => { void connection.stop(); };
  }, [orderId, userId]);

  const config = STATUS_CONFIG[status] ?? STATUS_CONFIG.PENDING;
  const isLive = status === 'PENDING' || status === 'PROCESSING';

  return (
    <CreditCounter
      label={t('orderStatus')}
      tone={config.tone}
      live={isLive}
      value={
        <span className="inline-flex items-center gap-2">
          <config.Icon size={16} className={status === 'PROCESSING' ? 'animate-spin' : ''} />
          {t(`status.${config.key}`)}
        </span>
      }
    />
  );
}
