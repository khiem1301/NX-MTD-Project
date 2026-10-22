import { Select } from 'antd';
import { useProductLookup } from '@/hooks/useOrderLookups';

type Props = {
  value?: number;
  onChange?: (productId: number | undefined) => void;
  placeholder?: string;
  allowClear?: boolean;
  width?: number;
  disabled?: boolean;
};

/** Chọn mã hàng đang dùng (tra cứu chung, không kèm giá). */
export function ProductSelect({ value, onChange, placeholder, allowClear, width = 280, disabled }: Props) {
  const products = useProductLookup();
  return (
    <Select
      showSearch
      allowClear={allowClear}
      disabled={disabled}
      value={value}
      onChange={onChange}
      placeholder={placeholder}
      optionFilterProp="label"
      loading={products.isLoading}
      style={{ width }}
      options={(products.data?.items ?? []).map((p) => ({ value: p.productId, label: `${p.productCode} - ${p.productName}` }))}
    />
  );
}
