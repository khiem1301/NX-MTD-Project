import { useState } from 'react';
import { Alert, Modal, Space, Typography, Upload, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { DownloadOutlined, InboxOutlined } from '@ant-design/icons';
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
const TEMPLATE_URL = '/templates/Mau_Nhap_Dinh_Muc.xlsx';
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
      <Space direction="vertical" size={0}>
        <StatusTag status={r.action} labels={MESSAGES.orders.specImport.action} />
        {r.error && <Typography.Text type="danger">{r.error}</Typography.Text>}
      </Space>
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
  const data = file ? preview.data : undefined;

  const handleImport = () => {
    if (!file) return;
    confirmDialog({
      title: LABELS.importOrderMaterialsConfirmTitle,
      content: LABELS.importOrderMaterialsConfirmBody,
      onOk: () =>
        doImport.mutateAsync(file).then((result) => {
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
        <Typography.Link href={TEMPLATE_URL} download>
          <DownloadOutlined /> {MESSAGES.orders.specImport.template}
        </Typography.Link>
        <Upload.Dragger
          maxCount={1}
          accept=".xlsx"
          disabled={preview.isPending}
          beforeUpload={(next) => {
            setFile(next);
            preview.mutate(next);
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
