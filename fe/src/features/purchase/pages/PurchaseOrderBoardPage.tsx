import { useState } from 'react';
import { PlusOutlined } from '@ant-design/icons';
import { Button, Card, Space } from 'antd';
import { useSearchParams } from 'react-router-dom';
import { QueryError } from '@/components/common/QueryError';
import { useTableQuery } from '@/hooks/useTableQuery';
import { PurchaseOrderFormModal } from '@/features/warehouse/components/PurchaseOrderFormModal';
import { PurchaseOrderModal } from '../components/PurchaseOrderModal';
import { PurchaseOrderTable } from '../components/PurchaseOrderTable';
import { GOODS_STATUS_OPTIONS, PAYMENT_STATUS_OPTIONS, PURCHASE_TEXT } from '../constants';
import { usePurchaseOrders } from '../hooks/usePurchase';
import type { PoGoodsStatus, PoPaymentStatus, PurchaseRole } from '../types';
import { DebouncedSearch } from '@/components/common/DebouncedSearch';
import { FilterSelect } from '@/components/common/FilterSelect';
import { PageCard } from '@/components/common/PageCard';

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
  const [creating, setCreating] = useState(false);

  return (
    <PageCard
      title={isManager ? PURCHASE_TEXT.ordersTitle : PURCHASE_TEXT.paymentsTitle}
      description={isManager ? PURCHASE_TEXT.ordersSubtitle : PURCHASE_TEXT.paymentsSubtitle}
      extra={
        isManager && (
          <Button type="primary" icon={<PlusOutlined />} onClick={() => setCreating(true)}>
            {PURCHASE_TEXT.createOrder}
          </Button>
        )
      }
    >
      <Card size="small">
        <Space direction="vertical" style={{ width: '100%' }}>
          <Space wrap>
            <DebouncedSearch
              allowClear
              placeholder={PURCHASE_TEXT.searchOrder}
              style={{ width: 340 }}
              onSearch={(value) => table.setFilters({ keyword: value.trim() || undefined })}
            />
            {isManager && (
              <FilterSelect
                value={table.filters.goodsStatus}
                placeholder={PURCHASE_TEXT.allGoodsStatus}
                style={{ width: 200 }}
                options={GOODS_STATUS_OPTIONS}
                onChange={(value: PoGoodsStatus | undefined) =>
                  table.setFilters({ goodsStatus: value })
                }
              />
            )}
            <FilterSelect
              value={table.filters.paymentStatus}
              placeholder={PURCHASE_TEXT.allPaymentStatus}
              style={{ width: 200 }}
              options={PAYMENT_STATUS_OPTIONS}
              onChange={(value: PoPaymentStatus | undefined) =>
                table.setFilters({ paymentStatus: value })
              }
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
      <PurchaseOrderModal role={role} />
      {isManager && <PurchaseOrderFormModal open={creating} onClose={() => setCreating(false)} />}
    </PageCard>
  );
}

/** UC23 — Quản đốc: theo dõi đơn mua, chọn nhà cung cấp, đặt hàng, hủy. */
export const PurchaseOrdersPage = () => <PurchaseOrderBoardPage role="manager" />;

/** UC34 — Kế toán: xác nhận thanh toán đơn mua đã đặt hàng. */
export const PurchasePaymentsPage = () => <PurchaseOrderBoardPage role="accountant" />;
