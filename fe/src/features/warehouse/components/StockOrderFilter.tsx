import { Select } from 'antd';
import { useOrderOptions } from '@/hooks/useLookups';
import { formatDate } from '@/utils/format';
import { LABELS, MATERIAL_ORDER_SCOPE } from '../constants';
import type { MaterialOrderScope } from '../types';

type StockOrderFilterProps = {
  orderId?: number;
  orderScope?: MaterialOrderScope;
  onChange: (value: { orderId?: number; orderScope?: MaterialOrderScope }) => void;
};

const NO_ORDER_OPTION = { value: MATERIAL_ORDER_SCOPE.NO_ORDER, label: LABELS.orderScopeNoOrder };

/** Một ô lọc cho tồn kho NVL: chọn một đơn hàng của khách, hoặc "không thuộc đơn hàng nào" (NVL dùng chung). */
export function StockOrderFilter({ orderId, orderScope, onChange }: StockOrderFilterProps) {
  const orders = useOrderOptions();
  return (
    <Select<number | MaterialOrderScope>
      showSearch
      allowClear
      value={orderScope ?? orderId}
      placeholder={LABELS.orderFilter}
      optionFilterProp="label"
      loading={orders.isLoading}
      style={{ width: 320 }}
      options={[
        NO_ORDER_OPTION,
        ...(orders.data ?? []).map((o) => ({
          value: o.orderId,
          label: `${o.orderCode} (giao ${formatDate(o.dueDate)})`,
        })),
      ]}
      onChange={(value) =>
        onChange(
          typeof value === 'string'
            ? { orderScope: value, orderId: undefined }
            : { orderId: value, orderScope: undefined },
        )
      }
    />
  );
}
