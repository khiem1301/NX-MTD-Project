import { useState } from 'react';
import { Select, Space } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { fill, formatQty } from '@/utils/format';

export type PickableOrderLine = {
  orderLineId: number;
  productCode: string;
  color: string;
  sizeCode: string;
  orderedQty?: number;
};

type OrderLinePickerProps = {
  lines: readonly PickableOrderLine[];
  value?: number;
  onChange: (orderLineId: number | undefined) => void;
  disabled?: boolean;
  loading?: boolean;
  /** Câu hiển thị ở ô mã hàng khi chưa chọn đơn. */
  disabledPlaceholder?: string;
};

const t = MESSAGES.common;

/**
 * Chọn dòng đơn theo hai bước: mã hàng rồi màu - size (DEV-ALL-68), để tổ trưởng và công nhân biết rõ đang may mã nào, màu
 * nào, size nào. Đơn chỉ có một mã hàng thì chọn sẵn mã đó.
 */
export function OrderLinePicker({
  lines,
  value,
  onChange,
  disabled,
  loading,
  disabledPlaceholder,
}: OrderLinePickerProps) {
  const [picked, setPicked] = useState<string>();
  const products = [...new Set(lines.map((l) => l.productCode))];
  const product =
    lines.find((l) => l.orderLineId === value)?.productCode ??
    (picked !== undefined && products.includes(picked) ? picked : undefined) ??
    (products.length === 1 ? products[0] : undefined);
  const colorSizes = lines.filter((l) => l.productCode === product);

  return (
    <Space wrap>
      <Select<string>
        showSearch
        style={{ width: 180 }}
        placeholder={disabled ? disabledPlaceholder : t.selectProduct}
        disabled={disabled}
        loading={loading}
        value={product}
        options={products.map((p) => ({ value: p, label: p }))}
        onChange={(p) => {
          setPicked(p);
          onChange(undefined);
        }}
      />
      <Select<number>
        showSearch
        optionFilterProp="label"
        style={{ width: 220 }}
        placeholder={product === undefined ? t.selectProductFirst : t.selectColorSize}
        disabled={disabled || product === undefined}
        value={value}
        options={colorSizes.map((l) => ({
          value: l.orderLineId,
          label:
            `${l.color} - ${l.sizeCode}` +
            (l.orderedQty === undefined ? '' : ` (${fill(t.orderedQty, formatQty(l.orderedQty))})`),
        }))}
        onChange={onChange}
      />
    </Space>
  );
}
