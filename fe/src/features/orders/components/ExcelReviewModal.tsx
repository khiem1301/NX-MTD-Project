import { useState } from 'react';
import { Alert, Button, Checkbox, Flex, Modal, Space, Typography, message } from 'antd';
import { DatabaseOutlined, ReloadOutlined } from '@ant-design/icons';
import { ReplaceImportOption } from '@/components/common/ReplaceImportOption';
import { MESSAGES } from '@/constants/messages';
import { useApiMutation } from '@/hooks/useApiMutation';
import { useMaterialOptions } from '@/hooks/useLookups';
import { materialApi } from '@/services/materialApi';
import { fill } from '@/utils/format';
import type { SaveMaterialPayload } from '@/types/material';
import { useExcelReview } from '../hooks/useExcelReview';
import { useExistingData, useReviewDbOptions } from '../hooks/useExistingData';
import type { ReviewScope } from '../api/orderApi';
import type { ExcelBundle } from '../types';
import { REVIEW_TABLES, incompleteMaterials, type ReviewTable } from '../utils/excelReview';
import { ExcelReviewTabs } from './ExcelReviewTabs';
import { MissingMaterialsPanel } from './MissingMaterialsPanel';
import { HintTip } from '@/components/common/HintTip';

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
  /** Mã hàng đang sửa; null = Thêm mới / nhập theo đơn. */
  productId: number | null;
  /** Đơn đang tạo / sửa: máy chủ kiểm bìa, NVL theo size và NVL đã có của đơn. */
  orderId?: number | null;
  /** Chỉ hiện các bảng này (chọn file cho một mục); bỏ trống = mọi bảng có trong file. */
  only?: ReviewTable[];
  /** DEV-ALL-155: form sửa mã hàng — hiện ô thay thế và giữ trạng thái khi xem lại. */
  allowReplace?: boolean;
  initialReplace?: boolean;
  /** Quản đốc gọi API riêng và không được thêm NVL mới vào danh mục (mặc định Admin). */
  scope?: ReviewScope;
  /** newMaterials: NVL chưa có mà admin chọn thêm vào danh mục khi lưu (rỗng = không thêm). */
  onConfirm: (bundle: ExcelBundle, newMaterials: SaveMaterialPayload[], replace: boolean) => void;
  onCancel: () => void;
};

/**
 * DEV-ALL-122/123/155: popup kiểm tra dữ liệu đọc từ file Excel nhiều sheet. Mỗi sheet một tab sửa trực tiếp; máy chủ kiểm lại sau
 * mỗi lần sửa (không ghi gì). NVL chưa có trong danh mục là cảnh báo; Xác nhận thì hỏi có thêm vào danh mục khi lưu không.
 * Form sửa mã hàng có thêm ô thay thế dữ liệu cũ như các chỗ nhập Excel khác.
 */
export function ExcelReviewModal({
  fileName,
  bundle,
  productId,
  orderId = null,
  only,
  allowReplace = false,
  initialReplace = false,
  scope = 'admin',
  onConfirm,
  onCancel,
}: ExcelReviewModalProps) {
  // Bảng nạp từ hệ thống (nút "Nạp dữ liệu đang có") cũng hiện thành tab dù file không có bảng đó.
  const [loadedTables, setLoadedTables] = useState<ReviewTable[]>([]);
  const shown = (t: ReviewTable) => presentTables(bundle).includes(t) || loadedTables.includes(t);
  const tables = REVIEW_TABLES.filter((t) => shown(t) && (!only || only.includes(t)));
  const missing = REVIEW_TABLES.filter((t) => !shown(t) && (!only || only.includes(t)));
  const [replace, setReplace] = useState(initialReplace);
  const review = useExcelReview(bundle, productId, tables, orderId, allowReplace && replace, scope);
  const { draft, counts, newMaterials } = review;
  const [active, setActive] = useState<ReviewTable | undefined>(tables[0]);
  const dbOptions = useReviewDbOptions(productId, scope);
  // Mở lại NVL ngừng sử dụng ngay trên dòng lỗi (chỉ Admin), rồi kiểm lại.
  const reopen = useApiMutation((code: string) => materialApi.reopen('admin', code), {
    invalidate: [['lookups'], ['warehouse']],
  });
  const handleReopen = (code: string) =>
    reopen.mutate(code, {
      onSuccess: () => {
        void message.success(fill(text.reopenedMaterial, code));
        review.recheck();
      },
    });
  const existing = useExistingData({
    productId,
    orderId,
    scope,
    tables: REVIEW_TABLES.filter((t) => !only || only.includes(t)),
  });
  const handleLoadExisting = async () => {
    const data = await existing.load();
    if (!data) return;
    const loaded = review.loadExisting(
      data,
      REVIEW_TABLES.filter((t) => !only || only.includes(t)),
    );
    setLoadedTables((prev) => [...new Set([...prev, ...loaded])]);
    setActive((a) => a ?? loaded[0]);
    void message.success(text.loadedExisting);
  };
  const [onlyErrors, setOnlyErrors] = useState(false);
  const materials = useMaterialOptions();
  const materialOptions = (materials.data ?? []).map((m) => ({
    value: m.materialCode,
    label: `${m.materialCode} · ${m.materialName}${m.color ? ` (${m.color})` : ''}`,
  }));
  // review.data: kết quả kiểm gần nhất, không mất trong lúc đang kiểm lại.
  const crossErrors = review.data?.spec.crossErrors ?? [];
  const removedNames =
    allowReplace && replace
      ? [...(review.data?.spec.removedNames ?? []), ...(review.data?.orderMaterials?.removed ?? [])]
      : [];
  const done = (materialsToAdd: SaveMaterialPayload[]) =>
    onConfirm(draft, materialsToAdd, allowReplace && replace);

  /** Xác nhận: NVL chưa có trong danh mục được thêm luôn khi Lưu, không hỏi lại. */
  const handleConfirm = () => {
    if (review.check.isError) {
      void message.error(review.check.error.message);
      return;
    }
    if (counts.error > 0) {
      setOnlyErrors(true);
      void message.error(fill(text.blockedByErrors, counts.error));
      return;
    }
    if (newMaterials.length === 0) return done([]);
    if (scope !== 'admin') {
      void message.error(
        fill(text.managerNewMaterial, newMaterials.map((m) => m.materialCode).join(', ')),
      );
      return;
    }
    const incomplete = incompleteMaterials(newMaterials);
    if (incomplete.length > 0) {
      void message.error(
        fill(text.missingInfoRequired, incomplete.map((m) => m.materialCode).join(', ')),
      );
      return;
    }
    done(newMaterials);
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
        <HintTip text={text.hint} />
        {allowReplace && (
          <ReplaceImportOption
            checked={replace}
            onChange={setReplace}
            disabled={review.check.isPending}
            removed={removedNames}
          />
        )}
        {review.check.error && (
          // Lỗi cả lần kiểm (vd mã hàng không có trong đơn, đơn đã đóng): các dòng chưa kiểm được nên không Xác nhận được.
          <Alert type="error" showIcon message={review.check.error.message} />
        )}
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
            missing={review.data?.missingMaterials ?? []}
            materials={newMaterials}
            onChange={review.updateMaterial}
          />
        )}
        <Flex justify="space-between" align="center" wrap gap={8}>
          <Typography.Text type="secondary">
            {missing.length > 0 &&
              fill(text.notInFile, missing.map((t) => text.tabs[t]).join(', '))}
          </Typography.Text>
          <Space size={16} wrap>
            {existing.available && (
              <Button
                size="small"
                icon={<DatabaseOutlined />}
                loading={existing.loading}
                onClick={() => void handleLoadExisting()}
              >
                {text.loadExisting}
              </Button>
            )}
            <Checkbox checked={onlyErrors} onChange={(e) => setOnlyErrors(e.target.checked)}>
              {text.onlyErrors}
            </Checkbox>
          </Space>
        </Flex>
        <ExcelReviewTabs
          review={review}
          tables={tables}
          active={active}
          onActiveChange={setActive}
          onlyErrors={onlyErrors}
          creating={productId === null}
          materialOptions={materialOptions}
          dbOptions={dbOptions}
          reopenMaterial={scope === 'admin' ? handleReopen : undefined}
          reopeningCode={reopen.isPending ? reopen.variables : undefined}
        />
      </Space>
    </Modal>
  );
}
