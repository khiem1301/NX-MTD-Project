import { Space, Typography } from 'antd';
import { fill, formatQty } from '@/utils/format';
import { LABELS } from '../constants';
import type { TeamFabricBalance } from '../utils/layRemaining';

type LayFabricBalanceProps = { balance: TeamFabricBalance };

/** Một dòng gọn: vải còn lại của tổ trên đơn và còn lại sau bàn này (DEV-ALL-90). */
export function LayFabricBalance({ balance }: LayFabricBalanceProps) {
  return (
    <Space size={16} wrap style={{ marginBottom: 12 }} data-testid="lay-fabric-balance">
      <span>
        {LABELS.teamFabricLeft}: <strong>{formatQty(balance.left)}</strong>
      </span>
      <span>
        {LABELS.teamFabricAfter}:{' '}
        <Typography.Text strong type={balance.after < 0 ? 'danger' : undefined}>
          {formatQty(balance.after)}
        </Typography.Text>
      </span>
      {balance.unmeasuredLays > 0 && (
        <Typography.Text type="warning">
          {fill(LABELS.teamFabricUnmeasured, balance.unmeasuredLays)}
        </Typography.Text>
      )}
    </Space>
  );
}
