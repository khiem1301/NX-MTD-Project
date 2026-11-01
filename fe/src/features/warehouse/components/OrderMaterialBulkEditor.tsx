import { useState } from 'react';
import { PlusOutlined, SearchOutlined } from '@ant-design/icons';
import { Button, Input, Space, Typography, message, theme } from 'antd';
import { useQueryClient } from '@tanstack/react-query';
import { DataTable } from '@/components/common/DataTable';
import { TableToolbar } from '@/components/common/TableToolbar';
import { MESSAGES } from '@/constants/messages';
import { useRolePath } from '@/hooks/useRolePath';
import { useMaterialOptions } from '@/hooks/useLookups';
import { warehouseApi } from '../api/warehouseApi';
import { WAREHOUSE_KEY } from '../hooks/useMaterials';
import type { OrderMaterial } from '../types';
import {
  EMPTY_DRAFT,
  isChanged,
  matchesKeyword,
  rowError,
  toLine,
  type BulkLine,
  type Draft,
} from './bulkOrderMaterial';
import { bulkOrderMaterialColumns } from './bulkOrderMaterialColumns';

type OrderMaterialBulkEditorProps = {
  orderId: number;
  materials: OrderMaterial[];
  /** Đóng chế độ sửa (sau khi lưu hoặc hủy). */
  onDone: () => void;
};

/** Bảng giãn theo màn hình, chỉ cuộn ngang khi màn hẹp hơn mức này. */
const MIN_TABLE_WIDTH = 1000;
/** Bảng cuộn bên trong để tiêu đề cột và nút Lưu luôn hiện. */
const TABLE_BODY_HEIGHT = 'max(240px, calc(100vh - 300px))';

let newLineSeq = 0;

/** Sửa cả đơn ngay trên trang: sửa số lượng, số lần giao, ghi chú của mọi NVL và thêm NVL mới; chỉ gửi những dòng có thay đổi. */
export function OrderMaterialBulkEditor({
  orderId,
  materials,
  onDone,
}: OrderMaterialBulkEditorProps) {
  const { token } = theme.useToken();
  const role = useRolePath();
  const queryClient = useQueryClient();
  const materialOptions = useMaterialOptions();
  const [lines, setLines] = useState<BulkLine[]>(() => materials.map(toLine));
  const [saving, setSaving] = useState(false);
  const [showErrors, setShowErrors] = useState(false);
  const [keyword, setKeyword] = useState('');

  const patch = (key: string, change: Partial<Draft>) =>
    setLines((prev) =>
      prev.map((l) => {
        if (l.key !== key) return l;
        const draft = { ...l.draft, ...change };
        if (draft.customerQty <= 0) draft.expectedDeliveryCount = null;
        return { ...l, draft };
      }),
    );

  const addLine = () =>
    setLines((prev) => [
      { key: `new-${++newLineSeq}`, materialId: null, draft: { ...EMPTY_DRAFT } },
      ...prev,
    ]);

  const changed = lines.filter(isChanged);

  const handleSave = async () => {
    setShowErrors(true);
    if (changed.some((l) => rowError(l))) {
      setKeyword(''); // dòng lỗi có thể đang bị ô tìm kiếm ẩn đi
      return;
    }
    if (changed.length === 0) {
      onDone();
      return;
    }
    setSaving(true);
    let saved = 0;
    try {
      for (const line of changed) {
        const d = line.draft;
        const body = {
          orderId,
          materialId: line.materialId as number,
          requiredQty: d.requiredQty,
          customerQty: d.customerQty,
          expectedDeliveryCount: d.customerQty > 0 ? d.expectedDeliveryCount : null,
          note: d.note.trim() || undefined,
        };
        if (line.original) {
          await warehouseApi.updateOrderMaterial(role, line.original.orderMaterialId, body);
        } else {
          await warehouseApi.createOrderMaterial(role, body);
        }
        saved++;
      }
      void message.success(MESSAGES.warehouse.orderMaterialSaved);
      onDone();
    } catch (error) {
      const text = error instanceof Error ? error.message : '';
      void message.error(
        saved > 0 ? `Đã lưu ${saved}/${changed.length} dòng, dòng tiếp theo lỗi: ${text}` : text,
      );
    } finally {
      setSaving(false);
      await queryClient.invalidateQueries({ queryKey: WAREHOUSE_KEY });
    }
  };

  const columns = bulkOrderMaterialColumns({
    materialOptions: materialOptions.data ?? [],
    usedMaterialIds: lines.flatMap((l) => (l.materialId === null ? [] : [l.materialId])),
    showErrors,
    onPatch: patch,
    onPickMaterial: (key, materialId) =>
      setLines((prev) => prev.map((l) => (l.key === key ? { ...l, materialId } : l))),
    onRemove: (key) => setLines((prev) => prev.filter((l) => l.key !== key)),
  });

  return (
    <>
      <TableToolbar
        filters={
          <Space>
            <Typography.Text type="secondary">{`${lines.length} NVL của đơn`}</Typography.Text>
            <Input
              allowClear
              prefix={<SearchOutlined />}
              placeholder={MESSAGES.common.search}
              value={keyword}
              onChange={(e) => setKeyword(e.target.value)}
              style={{ width: 240 }}
            />
            <Button icon={<PlusOutlined />} onClick={addLine} disabled={saving}>
              {MESSAGES.common.create}
            </Button>
          </Space>
        }
        actions={
          <Space>
            <Button onClick={onDone} disabled={saving}>
              {MESSAGES.common.cancel}
            </Button>
            <Button type="primary" loading={saving} onClick={() => void handleSave()}>
              {changed.length > 0
                ? `${MESSAGES.common.save} (${changed.length} dòng đổi)`
                : MESSAGES.common.save}
            </Button>
          </Space>
        }
      />
      <DataTable<BulkLine>
        rowKey="key"
        columns={columns}
        dataSource={lines.filter((l) => matchesKeyword(l, keyword, materialOptions.data ?? []))}
        pagination={false}
        scroll={{ x: MIN_TABLE_WIDTH, y: TABLE_BODY_HEIGHT }}
        onRow={(line) => ({
          style: isChanged(line) ? { background: token.colorPrimaryBg } : undefined,
        })}
      />
    </>
  );
}
