import { useState } from 'react';
import { PlusOutlined } from '@ant-design/icons';
import { Button, Modal, Space, Typography } from 'antd';
import { DataTable } from '@/components/common/DataTable';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { MESSAGES } from '@/constants/messages';
import { useMaterialOptions } from '@/hooks/useLookups';
import { LABELS } from '../constants';
import { useCreateOrderMaterials } from '../hooks/useOrderMaterials';
import type { OrderMaterial } from '../types';
import {
  applyDraftChange,
  EMPTY_DRAFT,
  rowError,
  toLinePayload,
  type BulkLine,
  type Draft,
} from './bulkOrderMaterial';
import { bulkOrderMaterialColumns } from './bulkOrderMaterialColumns';
import { CustomerPlanNotice } from './CustomerPlanNotice';
import { MaterialFormModal } from './MaterialFormModal';

type OrderMaterialExternalModalProps = {
  orderId: number;
  /** NVL đơn đã khai báo: không cho chọn lại vì mỗi NVL chỉ có một dòng trong đơn. */
  declared: OrderMaterial[];
  onClose: () => void;
};

/** Bảng giãn theo hộp thoại, chỉ cuộn ngang khi màn hẹp hơn mức này. */
const MIN_TABLE_WIDTH = 1000;

let lineSeq = 0;
const newLine = (materialId: number | null = null): BulkLine => ({
  key: `ext-${++lineSeq}`,
  materialId,
  draft: { ...EMPTY_DRAFT },
});

/**
 * Thêm NVL ngoài định mức vào đơn: nhiều dòng cùng lúc, mỗi dòng chọn NVL (hoặc tạo NVL mới) và nhập số cần, khách cấp,
 * số lần giao, ngày / khoảng ngày giao. Lưu một lần; một dòng lỗi thì không dòng nào được lưu.
 */
export function OrderMaterialExternalModal({
  orderId,
  declared,
  onClose,
}: OrderMaterialExternalModalProps) {
  const materials = useMaterialOptions();
  const save = useCreateOrderMaterials();
  const [lines, setLines] = useState<BulkLine[]>(() => [newLine()]);
  const [showErrors, setShowErrors] = useState(false);
  const [materialOpen, setMaterialOpen] = useState(false);

  const patchLine = (key: string, change: (l: BulkLine) => BulkLine) =>
    setLines((prev) => prev.map((l) => (l.key === key ? change(l) : l)));

  const handleOk = () => {
    setShowErrors(true);
    if (lines.some((l) => rowError(l))) return;
    save.mutate({ orderId, lines: lines.map(toLinePayload) }, { onSuccess: onClose });
  };

  const declaredIds = declared.map((om) => om.materialId);
  const columns = bulkOrderMaterialColumns({
    materialOptions: (materials.data ?? []).filter((m) => !declaredIds.includes(m.materialId)),
    usedMaterialIds: lines.flatMap((l) => (l.materialId === null ? [] : [l.materialId])),
    showErrors,
    onPatch: (key, change: Partial<Draft>) =>
      patchLine(key, (l) => ({ ...l, draft: applyDraftChange(l.draft, change) })),
    onPickMaterial: (key, materialId) => patchLine(key, (l) => ({ ...l, materialId })),
    onRemove: (key) => setLines((prev) => prev.filter((l) => l.key !== key)),
  });

  return (
    <Modal
      open
      width={MODAL_WIDTH.xl}
      title={LABELS.addExternalMaterial}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      okButtonProps={{ disabled: lines.length === 0 }}
      confirmLoading={save.isPending}
      onOk={handleOk}
      onCancel={onClose}
    >
      <CustomerPlanNotice orderId={orderId} />
      <Typography.Paragraph type="secondary">{LABELS.externalMaterialsHint}</Typography.Paragraph>
      <Space style={{ marginBottom: 12 }}>
        <Button icon={<PlusOutlined />} onClick={() => setLines((prev) => [...prev, newLine()])}>
          {LABELS.addLine}
        </Button>
        <Button type="link" icon={<PlusOutlined />} onClick={() => setMaterialOpen(true)}>
          {LABELS.addMaterial}
        </Button>
      </Space>
      <DataTable<BulkLine>
        rowKey="key"
        columns={columns}
        dataSource={lines}
        loading={materials.isLoading}
        pagination={false}
        scroll={{ x: MIN_TABLE_WIDTH }}
      />
      <MaterialFormModal
        open={materialOpen}
        material={null}
        onClose={() => setMaterialOpen(false)}
        onSaved={(m) => setLines((prev) => [...prev, newLine(m.materialId)])}
      />
    </Modal>
  );
}
