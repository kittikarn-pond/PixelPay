import { Logger } from '@nestjs/common';
import axios from 'axios';
import { hmacSha256 } from '../../common/utils/crypto';
import { IProviderAdapter, TopupParams, TopupResult } from './provider.interface';

export class UnipinAdapter implements IProviderAdapter {
  readonly slug = 'unipin';
  private readonly logger = new Logger(UnipinAdapter.name);

  constructor(
    private readonly apiUrl: string,
    private readonly apiKey: string,
    private readonly apiSecret: string,
  ) {}

  async processTopup(params: TopupParams): Promise<TopupResult> {
    const timestamp = Math.floor(Date.now() / 1000).toString();
    const signatureData = `${this.apiKey}${params.orderId}${params.providerSku}${params.gameUid}${timestamp}`;
    const signature = hmacSha256(this.apiSecret, signatureData);

    try {
      const response = await axios.post(
        `${this.apiUrl}/topup`,
        {
          partnerTrxId: params.orderId,
          itemCode: params.providerSku,
          userId: params.gameUid,
          serverId: params.gameServer ?? '',
          qty: params.quantity,
          timestamp,
          signature,
          apiKey: this.apiKey,
        },
        { timeout: 30000 },
      );

      const data = response.data;
      if (data.status === 'success' || data.rc === '00') {
        return {
          success: true,
          providerOrderId: data.trxId ?? data.orderId,
          providerReference: data.serialNumber ?? data.ref,
        };
      }

      return { success: false, failureReason: data.message ?? 'Provider returned failure' };
    } catch (error: any) {
      this.logger.error(`Unipin topup failed: ${error.message}`);
      return { success: false, failureReason: error.message };
    }
  }
}
