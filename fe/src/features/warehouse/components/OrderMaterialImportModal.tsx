import { useState } from 'react';
import { Alert, Modal, Space, Spin, Upload, message } from 'antd';
import { InboxOutlined } from '@ant-design/icons';
import { ImportTemplateLink } from '@/components/common/ImportTemplateLink';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { MESSAGES } from '@/constants/messages';
import { useApiMutation } from '@/hooks/useApiMutation';
import { useRolePath } from '@/hooks/useRolePath';
import type { SaveMaterialPayload } from '@/types/material';
import { excelBundleApi, type ReviewScope } from '@/features/orders/api/orderApi';
import { ExcelReviewModal } from '@/features/orders/components/ExcelReviewModal';
import type { ExcelBundle } from '@/features/orders/types';
import {
  createMaterials,
  refreshAfterImport,
  showImportResults,
} from '@/features/orders/utils/productExcelImports';
import { orderMaterialImportDoneMessage } from '@/features/orders/utils/importMessages';
import { LABELS } from '../constants';
import { WAREHOUSE_KEY } from '../hooks/useMaterials';
import { HintTip } from '@/components/common/HintTip';

type OrderMaterialImportModalProps = { orderId: number; onClose: () => void };

/** Bảng duy nhất của hộp thoại này; các sheet khác trong file (nếu có) không được đọc ở đây. */
const ONLY = ['orderMaterials'] as const;

/**
 * DEV-ALL-119/175: nhập NVL của đơn từ Excel (Mã NVL, Số cần, Khách cấp, Ghi chú). Chọn file là mở popup kiểm tra: sửa từng ô,
 * máy chủ kiểm lại sau mỗi lần sửa (Mới / Cập nhật / Không đổi / Lỗi, Phải mua tính sẵn); Xác nhận thì lưu các dòng hợp lệ một lần.
 * Chỉ gắn khi đang mở.
 */
export function OrderMaterialImportModal({ orderId, onClose }: OrderMaterialImportModalProps) {
  const role = useRolePath();
  const scope: ReviewScope = role === 'admin' ? 'admin' : 'manager';
  const [reviewing, setReviewing] = useState<{ bundle: ExcelBundle; fileName: string } | null>(
    null,
  );
  const parse = useApiMutation((file: File) => excelBundleApi.parse(file, scope), {
    invalidate: [],
  });
  const doImport = useApiMutation(
    (args: { bundle: ExcelBundle; replace: boolean }) =>
      excelBundleApi.importOrderMaterials(orderId, args.bundle.orderMaterials, args.replace, scope),
    { invalidate: [WAREHOUSE_KEY] },
  );

  const handleFile = (file: File) =>
    parse.mutate(file, {
      onSuccess: (bundle) => {
        if (!bundle.hasOrderMaterials) {
          void message.warning(LABELS.importOrderMaterialsNoTable);
          return;
        }
        setReviewing({ bundle, fileName: file.name });
      },
    });

  const handleConfirm = async (
    edited: ExcelBundle,
    materials: SaveMaterialPayload[],
    replace: boolean,
  ) => {
    if (doImport.isPending) return;
    if (materials.length > 0) {
      const created = await createMaterials(materials);
      if (!created.ok) {
        showImportResults(LABELS.importOrderMaterialsTitle, [created]);
        return;
      }
    }
    doImport.mutate(
      { bundle: edited, replace },
      {
        onSuccess: async (result) => {
          await refreshAfterImport();
          void message.success(orderMaterialImportDoneMessage(result.data, materials.length));
          setReviewing(null);
          onClose();
        },
      },
    );
  };

  return (
    <Modal
      open
      width={MODAL_WIDTH.xl}
      title={LABELS.importOrderMaterialsTitle}
      footer={null}
      onCancel={onClose}
      destroyOnHidden
    >
      <Space direction="vertical" size={16} style={{ width: '100%' }}>
        <HintTip text={LABELS.importOrderMaterialsHint} />
        <ImportTemplateLink kind="order-materials" orderId={orderId} />
        <Upload.Dragger
          maxCount={1}
          accept=".xlsx"
          showUploadList={false}
          disabled={parse.isPending || doImport.isPending}
          beforeUpload={(next) => {
            handleFile(next);
            return false;
          }}
        >
          <p className="ant-upload-drag-icon">
            <InboxOutlined />
          </p>
          <p>{MESSAGES.orders.specImport.chooseFile}</p>
        </Upload.Dragger>
        {(parse.isPending || doImport.isPending) && <Spin />}
        {parse.error && <Alert type="error" showIcon message={parse.error.message} />}
      </Space>
      {reviewing && (
        <ExcelReviewModal
          fileName={reviewing.fileName}
          bundle={reviewing.bundle}
          productId={null}
          orderId={orderId}
          only={[...ONLY]}
          allowReplace
          scope={scope}
          onConfirm={(bundle, materials, replace) => void handleConfirm(bundle, materials, replace)}
          onCancel={() => setReviewing(null)}
        />
      )}
    </Modal>
  );
}
