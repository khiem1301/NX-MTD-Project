import { useState } from 'react';
import { Button, Card, Select, Space } from 'antd';
import { PurchaseOrderFormModal } from '../components/PurchaseOrderFormModal';
import { StockPurchaseTable } from '../components/StockPurchaseTable';
import { LABELS, PO_GOODS_STATUS_LABEL } from '../constants';

type Kind = 'all' | 'stock' | 'order';

const KIND_OPTIONS = [
  { value: 'all', label: LABELS.poKindAll },
  { value: 'stock', label: LABELS.poKindStock },
  { value: 'order', label: LABELS.poKindOrder },
];

const STATUS_OPTIONS = Object.entries(PO_GOODS_STATUS_LABEL).map(([value, { text }]) => ({ value, label: text }));

/** Quản đốc theo dõi mọi đơn mua NVL (dự trữ và gắn đơn hàng); lọc theo loại và trạng thái hàng. */
export function PurchaseOrderListPage() {
  const [kind, setKind] = useState<Kind>('all');
  const [goodsStatus, setGoodsStatus] = useState<string>();
  const [formOpen, setFormOpen] = useState(false);

  // stockOnly: true = chỉ đơn dự trữ; false = chỉ đơn gắn đơn hàng; undefined = tất cả.
  const stockOnly = kind === 'all' ? undefined : kind === 'stock';

  return (
    <Card
      title={LABELS.purchaseOrdersTitle}
      extra={
        <Space wrap>
          <Select value={kind} onChange={setKind} options={KIND_OPTIONS} style={{ width: 260 }} />
          <Select
            allowClear
            placeholder={LABELS.poGoodsStatusFilter}
            value={goodsStatus}
            onChange={setGoodsStatus}
            options={STATUS_OPTIONS}
            style={{ width: 200 }}
          />
          <Button type="primary" onClick={() => setFormOpen(true)}>
            {LABELS.createStockPurchase}
          </Button>
        </Space>
      }
    >
      <StockPurchaseTable key={`${kind}-${goodsStatus ?? ''}`} stockOnly={stockOnly} goodsStatus={goodsStatus} />
      <PurchaseOrderFormModal open={formOpen} onClose={() => setFormOpen(false)} />
    </Card>
  );
}
