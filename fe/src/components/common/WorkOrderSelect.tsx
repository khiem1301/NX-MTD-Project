import { Select } from 'antd';
import type { WorkOrderListItem } from '@/types/workOrder';
import { formatDate } from '@/utils/format';

type WorkOrderSelectProps = {
  workOrders: WorkOrderListItem[] | undefined;
  loading?: boolean;
  value?: number;
  onChange?: (workOrderId: number | undefined) => void;
  placeholder?: string;
};

const WIDTH = 420;

/** Chọn lệnh cắt (danh sách do màn gọi truyền vào theo quyền của role). */
export function WorkOrderSelect({
  workOrders,
  loading,
  value,
  onChange,
  placeholder,
}: WorkOrderSelectProps) {
  return (
    <Select
      showSearch
      allowClear
      value={value}
      onChange={onChange}
      placeholder={placeholder}
      loading={loading}
      optionFilterProp="label"
      style={{ width: WIDTH }}
      options={(workOrders ?? []).map((w) => ({
        value: w.workOrderId,
        label: `${w.workOrderCode} · ${w.orderCode} · ${w.productCode} · hạn ${formatDate(w.dueDate)}`,
      }))}
    />
  );
}
