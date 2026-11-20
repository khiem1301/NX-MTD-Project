import { useState } from 'react';
import { Alert, Modal, Space, Typography, Upload, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { InboxOutlined } from '@ant-design/icons';
import { ReasonHover } from '@/components/common/ReasonHover';
import { ImportTemplateLink } from '@/components/common/ImportTemplateLink';
import { ReplaceImportOption } from '@/components/common/ReplaceImportOption';
import { DataTable } from '@/components/common/DataTable';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { confirmDialog } from '@/utils/confirmDialog';
import { fill, formatQty } from '@/utils/format';
import { LABELS } from '../constants';
import { useImportOrderMaterials, usePreviewOrderMaterialImport } from '../hooks/useOrderMaterials';
import type { OrderMaterialImportRow } from '../types';

/** File mẫu dùng chung với nhập định mức (sheet "NVL của đơn"). */
const qty = (v: number | null) => (v === null ? '—' : formatQty(v));

const columns: ColumnsType<OrderMaterialImportRow> = [
  { title: LABELS.importRow, dataIndex: 'rowNumber', width: 64 },
  { title: LABELS.materialCode, dataIndex: 'materialCode', width: 130 },
  {
    title: LABELS.materialName,
    key: 'name',
    render: (_, r) =>
      r.materialName ? `${r.materialName}${r.materialColor ? ` (${r.materialColor})` : ''}` : '—',
  },
  { title: LABELS.requiredQty, dataIndex: 'requiredQty', align: 'right', render: qty },
  { title: LABELS.customerQty, dataIndex: 'customerQty', align: 'right', render: qty },
  { title: LABELS.purchaseQty, dataIndex: 'purchaseQty', align: 'right', render: qty },
  { title: LABELS.note, dataIndex: 'note', render: (v: string | null) => v ?? '—' },
  {
    title: LABELS.importResult,
    key: 'result',
    width: 260,
    render: (_, r) => (
      <ReasonHover reasons={[r.error && { text: r.error, type: 'danger' }]}>
        <StatusTag status={r.action} labels={MESSAGES.orders.specImport.action} />
      </ReasonHover>
    ),
  },
];

type OrderMaterialImportModalProps = { orderId: number; onClose: () => void };

/**
 * DEV-ALL-119: nhập NVL của đơn từ Excel (Mã NVL, Số cần, Khách cấp, Ghi chú). Chọn file là xem trước (Mới / Cập nhật / Không
 * đổi / Lỗi, Phải mua tính sẵn); bấm Nhập thì lưu các dòng hợp lệ có thay đổi một lần. Chỉ gắn khi đang mở.
 */
export function OrderMaterialImportModal({ orderId, onClose }: OrderMaterialImportModalProps) {
  const preview = usePreviewOrderMaterialImport(orderId);
  const doImport = useImportOrderMaterials(orderId);
  const [file, setFile] = useState<File | null>(null);
  const [replace, setReplaceState] = useState(false);
  const data = file ? preview.data : undefined;

  const setReplace = (next: boolean) => {
    setReplaceState(next);
    if (file) preview.mutate({ file, replace: next });
  };

  const handleImport = () => {
    if (!file) return;
    const removed = replace ? (data?.removeCount ?? 0) : 0;
    confirmDialog({
      title: LABELS.importOrderMaterialsConfirmTitle,
      content:
        removed > 0
          ? `${LABELS.importOrderMaterialsConfirmBody} ${fill(MESSAGES.common.importReplace.confirmExtra, removed)}`
          : LABELS.importOrderMaterialsConfirmBody,
      onOk: () =>
        doImport.mutateAsync({ file, replace }).then((result) => {
          void message.success(result.message ?? MESSAGES.common.saved);
          onClose();
        }),
    });
  };

  return (
    <Modal
      open
      width={MODAL_WIDTH.xl}
      title={LABELS.importOrderMaterialsTitle}
      okText={LABELS.importOrderMaterialsConfirm}
      cancelText={MESSAGES.common.cancel}
      okButtonProps={{
        disabled: !data || data.changeCount === 0 || preview.isPending,
        loading: doImport.isPending,
      }}
      onOk={handleImport}
      onCancel={onClose}
      destroyOnHidden
    >
      <Space direction="vertical" size={16} style={{ width: '100%' }}>
        <Typography.Text type="secondary">{LABELS.importOrderMaterialsHint}</Typography.Text>
        <ImportTemplateLink kind="order-materials" orderId={orderId} />
        <ReplaceImportOption
          checked={replace}
          onChange={setReplace}
          disabled={preview.isPending || doImport.isPending}
          removed={data?.removed}
          blocked={data?.blocked}
          unit="NVL"
        />
        <Upload.Dragger
          maxCount={1}
          accept=".xlsx"
          disabled={preview.isPending}
          beforeUpload={(next) => {
            setFile(next);
            preview.mutate({ file: next, replace });
            return false;
          }}
          onRemove={() => {
            setFile(null);
            preview.reset();
          }}
        >
          <p className="ant-upload-drag-icon">
            <InboxOutlined />
          </p>
          <p>{MESSAGES.orders.specImport.chooseFile}</p>
        </Upload.Dragger>
        {preview.error && <Alert type="error" showIcon message={preview.error.message} />}
        {data && (
          <>
            <Typography.Text>
              {fill(LABELS.importSummary, data.validCount, data.errorCount, data.duplicateCount)}
            </Typography.Text>
            <DataTable<OrderMaterialImportRow>
              rowKey="rowNumber"
              size="small"
              columns={columns}
              dataSource={data.rows}
              scroll={{ x: 'max-content', y: 360 }}
            />
          </>
        )}
      </Space>
    </Modal>
  );
}
