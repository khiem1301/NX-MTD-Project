import { Select } from 'antd';
import { useOrderOptions } from '@/hooks/useLookups';
import { formatDate } from '@/utils/format';

type OrderSelectProps = {
  value?: number;
  onChange?: (orderId: number | undefined) => void;
  placeholder?: string;
  allowClear?: boolean;
  width?: number | string;
  /** Câu báo khi không có đơn nào để chọn. */
  notFoundContent?: string;
  /** Quản đốc / Admin chọn tổ may: chỉ hiện đơn có dòng chia cho tổ đó (DEV-ALL-68). */
  sewingTeamId?: number;
  disabled?: boolean;
};

const DEFAULT_WIDTH = 260;

/** Chọn đơn hàng đang mở (dropdown dùng chung nhiều màn). Người thuộc tổ may chỉ thấy đơn được chia cho tổ (BE lọc, DEV-ALL-49). */
export function OrderSelect({
  value,
  onChange,
  placeholder,
  allowClear,
  width,
  notFoundContent,
  sewingTeamId,
  disabled,
}: OrderSelectProps) {
  const orders = useOrderOptions(sewingTeamId);
  return (
    <Select
      showSearch
      allowClear={allowClear}
      disabled={disabled}
      value={value}
      onChange={onChange}
      placeholder={placeholder}
      notFoundContent={notFoundContent}
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
