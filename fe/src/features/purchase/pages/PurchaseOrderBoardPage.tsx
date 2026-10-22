import { Card, Input, Select, Space, Typography } from 'antd';
import { useSearchParams } from 'react-router-dom';
import { QueryError } from '@/components/common/QueryError';
import { useTableQuery } from '@/hooks/useTableQuery';
import { PurchaseOrderDrawer } from '../components/PurchaseOrderDrawer';
import { PurchaseOrderTable } from '../components/PurchaseOrderTable';
import { GOODS_STATUS_OPTIONS, PAYMENT_STATUS_OPTIONS, PURCHASE_TEXT } from '../constants';
import { usePurchaseOrders } from '../hooks/usePurchase';
import type { PoGoodsStatus, PoPaymentStatus, PurchaseRole } from '../types';

type Filters = { keyword?: string; goodsStatus?: PoGoodsStatus; paymentStatus?: PoPaymentStatus };

/**
 * Danh sách đơn mua dùng chung hai vai trò (BE chọn phạm vi): Quản đốc thấy mọi đơn mua và đặt hàng / hủy;
 * Kế toán chỉ thấy đơn đã đặt hàng và xác nhận thanh toán / trả lại (UC23, UC34).
 */
function PurchaseOrderBoardPage({ role }: { role: PurchaseRole }) {
  const [, setSearchParams] = useSearchParams();
  const table = useTableQuery<Filters>({});
  const orders = usePurchaseOrders(role, table.params);
  const isManager = role === 'manager';

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <div>
        <Typography.Title level={3} style={{ marginBottom: 4 }}>
          {isManager ? PURCHASE_TEXT.ordersTitle : PURCHASE_TEXT.paymentsTitle}
        </Typography.Title>
        <Typography.Text type="secondary">
          {isManager ? PURCHASE_TEXT.ordersSubtitle : PURCHASE_TEXT.paymentsSubtitle}
        </Typography.Text>
      </div>

      <Card size="small">
        <Space direction="vertical" style={{ width: '100%' }}>
          <Space wrap>
            <Input.Search
              allowClear
              placeholder={PURCHASE_TEXT.searchOrder}
              style={{ width: 340 }}
              onSearch={(value) => table.setFilters({ keyword: value.trim() || undefined })}
            />
            {isManager && (
              <Select
                allowClear
                placeholder={PURCHASE_TEXT.allGoodsStatus}
                style={{ width: 200 }}
                options={GOODS_STATUS_OPTIONS}
                onChange={(value: PoGoodsStatus | undefined) => table.setFilters({ goodsStatus: value })}
              />
            )}
            <Select
              allowClear
              placeholder={PURCHASE_TEXT.allPaymentStatus}
              style={{ width: 200 }}
              options={PAYMENT_STATUS_OPTIONS}
              onChange={(value: PoPaymentStatus | undefined) => table.setFilters({ paymentStatus: value })}
            />
          </Space>
          <QueryError error={orders.error} onRetry={() => void orders.refetch()} />
          <PurchaseOrderTable
            data={orders.data}
            loading={orders.isFetching}
            pagination={table.pagination(orders.data?.totalItems)}
            onOpen={(id) => setSearchParams({ id: String(id) })}
          />
        </Space>
      </Card>
      <PurchaseOrderDrawer role={role} />
    </Space>
  );
}

/** UC23 — Quản đốc: theo dõi đơn mua, chọn nhà cung cấp, đặt hàng, hủy. */
export const PurchaseOrdersPage = () => <PurchaseOrderBoardPage role="manager" />;

/** UC34 — Kế toán: xác nhận thanh toán đơn mua đã đặt hàng. */
export const PurchasePaymentsPage = () => <PurchaseOrderBoardPage role="accountant" />;
