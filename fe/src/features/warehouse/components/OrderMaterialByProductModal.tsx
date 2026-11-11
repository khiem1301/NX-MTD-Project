import { useState } from 'react';
import { Empty, Form, Modal, Select, Typography } from 'antd';
import { DataTable } from '@/components/common/DataTable';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { MESSAGES } from '@/constants/messages';
import { LABELS } from '../constants';
import { useCreateOrderMaterials, useOrderProductMaterials } from '../hooks/useOrderMaterials';
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

type OrderMaterialByProductModalProps = {
  orderId: number;
  /** NVL đơn đã khai báo: không hiện lại vì mỗi NVL chỉ có một dòng trong đơn. */
  declared: OrderMaterial[];
  onClose: () => void;
};

/** Bảng giãn theo hộp thoại, chỉ cuộn ngang khi màn hẹp hơn mức này. */
const MIN_TABLE_WIDTH = 1000;

/** Chọn mã hàng trong đơn rồi nhập số lượng cho các NVL định mức của mã đó; NVL không dùng thì bỏ khỏi bảng. */
export function OrderMaterialByProductModal({
  orderId,
  declared,
  onClose,
}: OrderMaterialByProductModalProps) {
  const products = useOrderProductMaterials(orderId);
  const save = useCreateOrderMaterials();
  const [pickedId, setPickedId] = useState<number>();
  const [drafts, setDrafts] = useState<Record<number, Draft>>({});
  const [removedIds, setRemovedIds] = useState<number[]>([]);
  const [showErrors, setShowErrors] = useState(false);

  const list = products.data ?? [];
  const product =
    list.find((p) => p.productId === pickedId) ?? (list.length === 1 ? list[0] : undefined);
  const notDeclared = (product?.materials ?? []).filter(
    (m) => !declared.some((om) => om.materialId === m.materialId),
  );
  const lines: BulkLine[] = notDeclared
    .filter((m) => !removedIds.includes(m.materialId))
    .map((m) => ({
      key: `m-${m.materialId}`,
      materialId: m.materialId,
      draft: drafts[m.materialId] ?? EMPTY_DRAFT,
    }));

  const pickProduct = (productId: number) => {
    setPickedId(productId);
    setDrafts({});
    setRemovedIds([]);
    setShowErrors(false);
  };

  const handleOk = () => {
    setShowErrors(true);
    if (lines.some((l) => rowError(l))) return;
    save.mutate({ orderId, lines: lines.map(toLinePayload) }, { onSuccess: onClose });
  };

  const columns = bulkOrderMaterialColumns({
    materialOptions: product?.materials ?? [],
    usedMaterialIds: [],
    lockMaterial: true,
    showErrors,
    onPatch: (key, change) => {
      const line = lines.find((l) => l.key === key);
      if (!line?.materialId) return;
      const { materialId, draft } = line;
      setDrafts((prev) => ({ ...prev, [materialId]: applyDraftChange(draft, change) }));
    },
    onPickMaterial: () => undefined,
    onRemove: (key) => {
      const materialId = lines.find((l) => l.key === key)?.materialId;
      if (materialId) setRemovedIds((prev) => [...prev, materialId]);
    },
  });

  const emptyText =
    (product?.materials.length ?? 0) > 0 ? LABELS.productAllDeclared : LABELS.productNoNorm;

  return (
    <Modal
      open
      width={MODAL_WIDTH.xl}
      title={LABELS.addByProduct}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      okButtonProps={{ disabled: lines.length === 0 }}
      confirmLoading={save.isPending}
      onOk={handleOk}
      onCancel={onClose}
    >
      <CustomerPlanNotice orderId={orderId} />
      <Form layout="vertical">
        <Form.Item label={LABELS.product} required>
          <Select
            showSearch
            optionFilterProp="label"
            placeholder={LABELS.selectProduct}
            loading={products.isLoading}
            value={product?.productId}
            options={list.map((p) => ({
              value: p.productId,
              label: `${p.productCode} - ${p.productName}`,
            }))}
            onChange={pickProduct}
          />
        </Form.Item>
      </Form>
      {product && notDeclared.length === 0 && <Empty description={emptyText} />}
      {notDeclared.length > 0 && (
        <>
          <Typography.Paragraph type="secondary">
            {LABELS.productMaterialsHint}
          </Typography.Paragraph>
          <DataTable<BulkLine>
            rowKey="key"
            columns={columns}
            dataSource={lines}
            pagination={false}
            scroll={{ x: MIN_TABLE_WIDTH }}
          />
        </>
      )}
    </Modal>
  );
}
