import { Spin, Tabs } from 'antd';
import { Suspense } from 'react';
import { useSearchParams } from 'react-router-dom';
import { ORDER_DETAIL_TABS } from '@/app/orderDetailTabs';
import { AuditHistoryTable } from '@/components/common/AuditHistoryTable';
import { MESSAGES } from '@/constants/messages';
import { ORDER_CLOSED_STATUSES, type OrderStatus } from '@/constants/orderStatus';
import type { OrderDetail, OrderDisplayLine } from '../types';
import { OrderMarkersTab } from './OrderMarkersTab';
import { OrderReadOnlyGrid } from './OrderReadOnlyGrid';

const GRID_TAB = 'grid';
const MARKERS_TAB = 'markers';
const HISTORY_TAB = 'history';

type OrderDetailTabsProps = {
  orderId: number;
  status: OrderStatus;
  lines: OrderDisplayLine[];
  /** Không truyền thì không có tab Lịch sử (lịch sử có đơn giá, chỉ Admin xem). */
  history?: OrderDetail['history'];
};

/** Các tab của chi tiết đơn; tab đang mở nằm trên đường dẫn (?tab=) để thông báo và nút Mở đơn mở thẳng tab NVL. */
export function OrderDetailTabs({ orderId, status, lines, history }: OrderDetailTabsProps) {
  const [params, setParams] = useSearchParams();
  const isClosed = ORDER_CLOSED_STATUSES.includes(status);

  const items = [
    {
      key: GRID_TAB,
      label: MESSAGES.orders.gridTab,
      children: <OrderReadOnlyGrid lines={lines} />,
    },
    {
      key: MARKERS_TAB,
      label: MESSAGES.orders.markersTab,
      children: <OrderMarkersTab orderId={orderId} isClosed={isClosed} />,
    },
    ...ORDER_DETAIL_TABS.map((t) => ({
      key: t.key,
      label: t.label,
      children: (
        <Suspense fallback={<Spin />}>
          <t.Component orderId={orderId} isClosed={isClosed} />
        </Suspense>
      ),
    })),
    ...(history
      ? [
          {
            key: HISTORY_TAB,
            label: MESSAGES.common.history,
            children: <AuditHistoryTable history={history} />,
          },
        ]
      : []),
  ];
  const activeKey = items.find((i) => i.key === params.get('tab'))?.key ?? GRID_TAB;

  return (
    <Tabs
      size="small"
      items={items}
      activeKey={activeKey}
      onChange={(tab) => setParams({ tab }, { replace: true })}
    />
  );
}
