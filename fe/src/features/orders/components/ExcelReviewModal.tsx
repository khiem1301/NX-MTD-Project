import { useState } from 'react';
import { Alert, Button, Checkbox, Flex, Modal, Space, Typography, message } from 'antd';
import { ReloadOutlined } from '@ant-design/icons';
import { MESSAGES } from '@/constants/messages';
import { useMaterialOptions } from '@/hooks/useLookups';
import { fill } from '@/utils/format';
import type { SaveMaterialPayload } from '@/types/material';
import { useExcelReview } from '../hooks/useExcelReview';
import type { ExcelBundle } from '../types';
import { REVIEW_TABLES, incompleteMaterials, type ReviewTable } from '../utils/excelReview';
import { AddMaterialsAskModal } from './AddMaterialsAskModal';
import { ExcelReviewTabs } from './ExcelReviewTabs';
import { MissingMaterialsPanel } from './MissingMaterialsPanel';

const text = MESSAGES.orders.excelReview;
const MODAL_WIDTH = 1180;

/** Bảng có trong file (cờ Has…) — tab hiện ra; bảng không có thì liệt kê ở dòng "Không có trong file". */
const presentTables = (b: ExcelBundle): ReviewTable[] => {
  const has: Record<ReviewTable, boolean> = {
    products: b.hasProducts,
    components: b.spec.hasComponents,
    norms: b.spec.hasNorms,
    groups: b.spec.hasMarkerGroups,
    markers: b.spec.hasOrderMarkers,
    orderMaterials: b.hasOrderMaterials,
  };
  return REVIEW_TABLES.filter((t) => has[t]);
};

type ExcelReviewModalProps = {
  fileName: string;
  bundle: ExcelBundle;
  /** Mã hàng đang sửa; null = Thêm mới. */
  productId: number | null;
  /** Chỉ hiện các bảng này (chọn file cho một mục); bỏ trống = mọi bảng có trong file. */
  only?: ReviewTable[];
  /** newMaterials: NVL chưa có mà admin chọn thêm vào danh mục khi lưu (rỗng = không thêm). */
  onConfirm: (bundle: ExcelBundle, newMaterials: SaveMaterialPayload[]) => void;
  onCancel: () => void;
};

/**
 * DEV-ALL-122/123: popup kiểm tra dữ liệu đọc từ file Excel nhiều sheet. Mỗi sheet một tab sửa trực tiếp; máy chủ kiểm lại sau
 * mỗi lần sửa (không ghi gì). NVL chưa có trong danh mục là cảnh báo; Xác nhận thì hỏi có thêm vào danh mục khi lưu không.
 */
export function ExcelReviewModal({
  fileName,
  bundle,
  productId,
  only,
  onConfirm,
  onCancel,
}: ExcelReviewModalProps) {
  const tables = presentTables(bundle).filter((t) => !only || only.includes(t));
  const missing = REVIEW_TABLES.filter(
    (t) => !presentTables(bundle).includes(t) && (!only || only.includes(t)),
  );
  const review = useExcelReview(bundle, productId, tables);
  const { draft, counts, newMaterials } = review;
  const [active, setActive] = useState<ReviewTable | undefined>(tables[0]);
  const [onlyErrors, setOnlyErrors] = useState(false);
  const [asking, setAsking] = useState(false);
  const materials = useMaterialOptions();
  const materialOptions = (materials.data ?? []).map((m) => ({
    value: m.materialName,
    label: `${m.materialName}${m.color ? ` (${m.color})` : ''}`,
  }));
  const crossErrors = review.check.data?.spec.crossErrors ?? [];

  const handleConfirm = () => (newMaterials.length === 0 ? onConfirm(draft, []) : setAsking(true));
  const handleAdd = () => {
    setAsking(false);
    const incomplete = incompleteMaterials(newMaterials);
    if (incomplete.length > 0) {
      void message.error(
        fill(text.missingInfoRequired, incomplete.map((m) => m.materialName).join(', ')),
      );
      return;
    }
    onConfirm(draft, newMaterials);
  };

  return (
    <Modal
      open
      width={MODAL_WIDTH}
      title={fill(text.title, fileName)}
      onCancel={onCancel}
      destroyOnHidden
      maskClosable={false}
      footer={
        <Flex justify="space-between" align="center" wrap gap={8}>
          <Typography.Text type="secondary">
            {review.check.isPending
              ? text.checking
              : fill(text.summary, counts.valid, counts.error, counts.pending, counts.newMaterial)}
          </Typography.Text>
          <Space>
            <Button onClick={onCancel}>{MESSAGES.common.cancel}</Button>
            <Button
              icon={<ReloadOutlined />}
              loading={review.check.isPending}
              onClick={review.recheck}
            >
              {text.recheck}
            </Button>
            <Button type="primary" onClick={handleConfirm}>
              {text.confirm}
            </Button>
          </Space>
        </Flex>
      }
    >
      <Space direction="vertical" size={12} style={{ width: '100%' }}>
        <Typography.Text type="secondary">{text.hint}</Typography.Text>
        {crossErrors.length > 0 && (
          <Alert
            type="error"
            showIcon
            message={text.crossErrors}
            description={crossErrors.map((e) => (
              <div key={e}>{e}</div>
            ))}
          />
        )}
        {newMaterials.length > 0 && (
          <MissingMaterialsPanel
            missing={review.check.data?.missingMaterials ?? []}
            materials={newMaterials}
            onChange={review.updateMaterial}
          />
        )}
        <Flex justify="space-between" align="center" wrap gap={8}>
          <Typography.Text type="secondary">
            {missing.length > 0 &&
              fill(text.notInFile, missing.map((t) => text.tabs[t]).join(', '))}
          </Typography.Text>
          <Checkbox checked={onlyErrors} onChange={(e) => setOnlyErrors(e.target.checked)}>
            {text.onlyErrors}
          </Checkbox>
        </Flex>
        <ExcelReviewTabs
          review={review}
          tables={tables}
          active={active}
          onActiveChange={setActive}
          onlyErrors={onlyErrors}
          creating={productId === null}
          materialOptions={materialOptions}
        />
      </Space>
      <AddMaterialsAskModal
        open={asking}
        codes={newMaterials.map((m) => m.materialName ?? '')}
        onAdd={handleAdd}
        onSkip={() => {
          setAsking(false);
          onConfirm(draft, []);
        }}
        onClose={() => setAsking(false)}
      />
    </Modal>
  );
}
