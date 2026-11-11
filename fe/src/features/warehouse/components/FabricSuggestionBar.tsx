import { Button, Select, Space, Typography, message, theme, type FormInstance } from 'antd';
import { useState } from 'react';
import { fill } from '@/utils/format';
import { LABELS } from '../constants';
import { useFabricNeedsFetcher, useWorkOrderLines } from '../hooks/useIssues';
import type { FabricRequirement } from '@/types/workOrder';
import { suggestFabricQty } from '../utils/issueSuggestion';

type FabricSuggestionBarProps = {
  form: FormInstance;
  workOrderId: number | undefined;
  fabrics: FabricRequirement[];
};

const SELECT_WIDTH = 140;

/** Đề xuất số vải cấp theo màu (+ size) của lệnh cắt: nhu cầu tính từ bìa / định mức, không vượt số khả dụng trong kho. */
export function FabricSuggestionBar({ form, workOrderId, fabrics }: FabricSuggestionBarProps) {
  const { token } = theme.useToken();
  const info = useWorkOrderLines(workOrderId);
  const fetchNeeds = useFabricNeedsFetcher(workOrderId);
  const [color, setColor] = useState<string>();
  const [sizeCode, setSizeCode] = useState<string>();
  const [loading, setLoading] = useState(false);

  const lines = info.data?.lines ?? [];
  const colors = [...new Set(lines.map((l) => l.color))];
  const sizes = [...new Set(lines.filter((l) => l.color === color).map((l) => l.sizeCode))];

  const apply = async () => {
    if (!color) return;
    setLoading(true);
    try {
      const needs = await fetchNeeds(color, sizeCode);
      const need = new Map(needs.map((n) => [n.materialId, n.needMeters]));
      const available = new Map(fabrics.map((f) => [f.materialId, f.availableQty]));
      const current = (form.getFieldValue('lines') ?? []) as {
        materialId: number;
        issuedQty?: number;
      }[];
      let applied = 0;
      const next = current.map((l) => {
        const qty = suggestFabricQty(need.get(l.materialId) ?? 0, available.get(l.materialId) ?? 0);
        if (qty <= 0) return l;
        applied += 1;
        return { ...l, issuedQty: qty };
      });
      form.setFieldValue('lines', next);
      if (applied === 0) void message.info(LABELS.suggestNothing);
      else void message.success(fill(LABELS.suggestApplied, applied));
    } finally {
      setLoading(false);
    }
  };

  return (
    <Space
      wrap
      size={8}
      align="center"
      style={{
        marginBottom: 12,
        padding: '6px 10px',
        borderRadius: token.borderRadius,
        background: token.colorFillAlter,
      }}
    >
      <Typography.Text strong>{LABELS.suggestTitle}</Typography.Text>
      <Typography.Text type="secondary">
        {LABELS.suggestProduct}: {info.data?.productCode ?? '—'}
      </Typography.Text>
      <Select
        size="small"
        placeholder={LABELS.suggestColor}
        value={color}
        onChange={(v) => {
          setColor(v);
          setSizeCode(undefined);
        }}
        options={colors.map((c) => ({ value: c, label: c }))}
        style={{ width: SELECT_WIDTH }}
      />
      <Select
        size="small"
        allowClear
        placeholder={LABELS.suggestSize}
        value={sizeCode}
        onChange={setSizeCode}
        options={sizes.map((s) => ({ value: s, label: s }))}
        disabled={!color}
        style={{ width: SELECT_WIDTH }}
      />
      <Button size="small" onClick={() => void apply()} disabled={!color} loading={loading}>
        {LABELS.suggestFabricAction}
      </Button>
    </Space>
  );
}
