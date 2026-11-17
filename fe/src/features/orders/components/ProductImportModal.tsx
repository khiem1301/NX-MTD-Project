import { useState } from 'react';
import { Alert, Modal, Space, Typography, Upload, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { DownloadOutlined, InboxOutlined } from '@ant-design/icons';
import { DataTable } from '@/components/common/DataTable';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { confirmDialog } from '@/utils/confirmDialog';
import { fill } from '@/utils/format';
import { SPEC_IMPORT_TEMPLATE_URL } from '../constants';
import { usePreviewProductImport, useProductImport } from '../hooks/useSpecImport';
import type { ProductImportRow } from '../types';

const text = MESSAGES.orders.productImport;
const col = text.columns;

const columns: ColumnsType<ProductImportRow> = [
  { title: col.row, dataIndex: 'rowNumber', width: 64 },
  { title: col.code, dataIndex: 'productCode', width: 140 },
  { title: col.name, dataIndex: 'productName' },
  { title: col.description, dataIndex: 'description', render: (v: string | null) => v ?? '—' },
  {
    title: col.result,
    key: 'result',
    width: 240,
    render: (_, r) => (
      <Space direction="vertical" size={0}>
        <StatusTag status={r.action} labels={text.action} />
        {r.note && (
          <Typography.Text type={r.action === 'Error' ? 'danger' : 'secondary'}>
            {r.note}
          </Typography.Text>
        )}
      </Space>
    ),
  },
];

type ProductImportModalProps = { onClose: () => void };

/**
 * DEV-ALL-119: tạo nhiều mã hàng một lần từ Excel. Chọn file là xem trước (Mới / Đã có / Lỗi); bấm Tạo mã hàng thì tạo các mã mới
 * hợp lệ trong một lần, mã đã có bỏ qua. Mã mới chọn được ngay ở form đơn hàng. Chỉ gắn khi đang mở.
 */
export function ProductImportModal({ onClose }: ProductImportModalProps) {
  const preview = usePreviewProductImport();
  const doImport = useProductImport();
  const [file, setFile] = useState<File | null>(null);
  const data = file ? preview.data : undefined;

  const handleImport = () => {
    if (!file) return;
    confirmDialog({
      title: text.confirmTitle,
      content: text.confirmBody,
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
      width={MODAL_WIDTH.lg}
      title={text.title}
      okText={text.confirm}
      cancelText={MESSAGES.common.cancel}
      okButtonProps={{
        disabled: !data || data.newCount === 0 || preview.isPending,
        loading: doImport.isPending,
      }}
      onOk={handleImport}
      onCancel={onClose}
      destroyOnHidden
    >
      <Space direction="vertical" size={16} style={{ width: '100%' }}>
        <Typography.Text type="secondary">{text.hint}</Typography.Text>
        <Typography.Link href={SPEC_IMPORT_TEMPLATE_URL} download>
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
              {fill(
                text.summary,
                data.newCount,
                data.existsCount,
                data.errorCount,
                data.duplicateCount,
              )}
            </Typography.Text>
            <DataTable<ProductImportRow>
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
