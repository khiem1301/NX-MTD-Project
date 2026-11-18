import { Select } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { fill, formatDate } from '@/utils/format';
import { useProductOrders } from '../hooks/useSpecImport';

const SELECT_WIDTH = 280;

type ProductOrderSelectProps = {
  productId: number;
  value: number | undefined;
  onChange: (orderId: number | undefined) => void;
  disabled?: boolean;
};

/** DEV-ALL-121: chọn đơn đang chạy có mã hàng này để nhập Bìa cắt, NVL của đơn khi Sửa mã hàng. */
export function ProductOrderSelect({
  productId,
  value,
  onChange,
  disabled,
}: ProductOrderSelectProps) {
  const orders = useProductOrders(productId);
  return (
    <Select
      allowClear
      showSearch
      size="small"
      optionFilterProp="label"
      style={{ width: SELECT_WIDTH }}
      placeholder={MESSAGES.orders.productExcel.orderPlaceholder}
      notFoundContent={MESSAGES.orders.specImport.noOrders}
      loading={orders.isLoading}
      disabled={disabled}
      value={value}
      onChange={onChange}
      options={(orders.data?.items ?? []).map((o) => ({
        value: o.orderId,
        label: fill(MESSAGES.orders.specImport.orderOption, o.orderCode, formatDate(o.dueDate)),
      }))}
    />
  );
}
