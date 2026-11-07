import { Button, Select, Space, Typography, message } from 'antd';
import { useState } from 'react';
import type { OrderLineOption } from '@/types/lookup';
import { fill } from '@/utils/format';
import { LABELS } from '../constants';
import { suggestPieceLines, type PieceSuggestion } from '../utils/issueSuggestion';

type PieceSuggestionBarProps = {
  lines: OrderLineOption[];
  available: ReadonlyMap<number, number>;
  taken: ReadonlySet<string>;
  onAdd: (suggestions: PieceSuggestion[]) => void;
};

const SELECT_WIDTH = 150;

const distinct = (values: string[]) => [...new Set(values)];

/** Đề xuất dòng cấp BTP theo mã hàng + màu (+ size): số cấp = min(khả dụng trong kho BTP, số đặt). */
export function PieceSuggestionBar({ lines, available, taken, onAdd }: PieceSuggestionBarProps) {
  const [productCode, setProductCode] = useState<string>();
  const [color, setColor] = useState<string>();
  const [sizeCode, setSizeCode] = useState<string>();

  const ofProduct = lines.filter((l) => l.productCode === productCode);
  const colors = distinct(ofProduct.map((l) => l.color));
  const sizes = distinct(ofProduct.filter((l) => l.color === color).map((l) => l.sizeCode));

  const apply = () => {
    if (!productCode || !color) return;
    const suggestions = suggestPieceLines(
      lines,
      available,
      { productCode, color, sizeCode },
      taken,
    );
    if (suggestions.length === 0) {
      void message.info(LABELS.suggestNothing);
      return;
    }
    onAdd(suggestions);
    void message.success(fill(LABELS.suggestApplied, suggestions.length));
  };

  return (
    <Space wrap align="center" style={{ marginBottom: 12 }}>
      <Typography.Text strong>{LABELS.suggestTitle}</Typography.Text>
      <Select
        placeholder={LABELS.suggestProduct}
        value={productCode}
        onChange={(v) => {
          setProductCode(v);
          setColor(undefined);
          setSizeCode(undefined);
        }}
        options={distinct(lines.map((l) => l.productCode)).map((c) => ({ value: c, label: c }))}
        style={{ width: SELECT_WIDTH }}
      />
      <Select
        placeholder={LABELS.suggestColor}
        value={color}
        onChange={(v) => {
          setColor(v);
          setSizeCode(undefined);
        }}
        options={colors.map((c) => ({ value: c, label: c }))}
        disabled={!productCode}
        style={{ width: SELECT_WIDTH }}
      />
      <Select
        allowClear
        placeholder={LABELS.suggestSize}
        value={sizeCode}
        onChange={setSizeCode}
        options={sizes.map((s) => ({ value: s, label: s }))}
        disabled={!color}
        style={{ width: SELECT_WIDTH }}
      />
      <Button onClick={apply} disabled={!color}>
        {LABELS.suggestPieceAction}
      </Button>
    </Space>
  );
}
