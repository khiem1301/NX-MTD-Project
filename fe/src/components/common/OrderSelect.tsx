import { Select } from 'antd';
import { useOrderOptions } from '@/hooks/useLookups';
import { formatDate } from '@/utils/format';

type OrderSelectProps = {
  value?: number;
  onChange?: (orderId: number | undefined) => void;
  placeholder?: string;
  allowClear?: boolean;
  width?: number | string;
};

const DEFAULT_WIDTH = 260;

/** Chọn đơn hàng đang mở (dropdown dùng chung nhiều màn). */
export function OrderSelect({ value, onChange, placeholder, allowClear, width }: OrderSelectProps) {
  const orders = useOrderOptions();
  return (
    <Select
      showSearch
      allowClear={allowClear}
      value={value}
      onChange={onChange}
      placeholder={placeholder}
      optionFilterProp="label"
      loading={orders.isLoading}
      style={{ width: width ?? DEFAULT_WIDTH }}
      options={(orders.data ?? []).map((o) => ({
        value: o.orderId,
        label: `${o.orderCode} (giao ${formatDate(o.dueDate)})`,
      }))}
    />
  );
}
