import { Typography } from 'antd';
import { formatCurrency } from '@/utils/format';
import { HOME_TEXT } from '../../constants';
import type { HomeMoney } from '../../types';
import { percentVsLast } from '../../utils/homeMoney';
import { StatTile } from './StatTile';

/** Ô tiền tháng này, kèm ↑ / ↓ so tháng trước. `upIsGood`: tăng là tốt (doanh thu) hay xấu (chi phí). */
export function MoneyTile({
  label,
  money,
  upIsGood,
  to,
}: {
  label: string;
  money: HomeMoney;
  upIsGood: boolean;
  to?: string;
}) {
  const percent = percentVsLast(money);
  const isUp = percent !== null && percent >= 0;
  const good = isUp === upIsGood;
  return (
    <StatTile
      label={label}
      to={to}
      value={money.thisMonth === null ? '—' : formatCurrency(money.thisMonth)}
      note={
        money.thisMonth === null ? (
          <Typography.Text type="secondary">{HOME_TEXT.costNotEntered}</Typography.Text>
        ) : (
          percent !== null && (
            <Typography.Text type={good ? 'success' : 'danger'}>
              {isUp ? '↑' : '↓'} {Math.abs(percent)}% {HOME_TEXT.vsLastMonth}
            </Typography.Text>
          )
        )
      }
    />
  );
}
