import { useState } from 'react';
import { Alert, Modal, Space, Tag, Typography, Upload } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { DownloadOutlined, InboxOutlined } from '@ant-design/icons';
import { DataTable } from '@/components/common/DataTable';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { MESSAGES } from '@/constants/messages';
import { fill, formatCurrency, formatQty } from '@/utils/format';
import { SPEC_IMPORT_TEMPLATE_URL } from '../constants';
import { usePreviewOrderImport } from '../hooks/useSpecImport';
import type { OrderImportRow } from '../types';

const text = MESSAGES.orders.orderImport;
const col = text.columns;

type OrderImportModalProps = {
  onApply: (rows: OrderImportRow[]) => void;
  onClose: () => void;
};

const columnsFor = (sizes: string[]): ColumnsType<OrderImportRow> => [
  { title: col.row, dataIndex: 'rowNumber', width: 64 },
  {
    title: col.product,
    key: 'product',
    render: (_, r) => (r.productName ? `${r.productCode} · ${r.productName}` : r.productCode),
  },
  { title: col.color, dataIndex: 'color', width: 110 },
  {
    title: col.price,
    dataIndex: 'cmtUnitPrice',
    align: 'right',
    width: 120,
    render: (v: number | null) => (v === null ? '—' : formatCurrency(v)),
  },
  ...sizes.map((size) => ({
    title: size,
    key: `size-${size}`,
    align: 'right' as const,
    width: 70,
    render: (_: unknown, r: OrderImportRow) =>
      formatQty(r.quantities.find((q) => q.sizeCode === size)?.qty ?? null),
  })),
  {
    title: col.result,
    key: 'result',
    width: 260,
    render: (_, r) => (
      <Space direction="vertical" size={0}>
        <Tag color={r.error ? text.invalid.color : text.valid.color}>
          {r.error ? text.invalid.text : text.valid.text}
        </Tag>
        {r.error && <Typography.Text type="danger">{r.error}</Typography.Text>}
      </Space>
    ),
  },
];

/**
 * DEV-ALL-118: nhập dòng hàng của đơn từ Excel dạng ma trận màu × size. Chọn file là xem trước (máy chủ kiểm mã hàng, số lượng,
 * đơn giá); Áp dụng thì điền các dòng hợp lệ vào form, người dùng xem lại và bấm Lưu như tạo tay. Chỉ gắn khi đang mở.
 */
export function OrderImportModal({ onApply, onClose }: OrderImportModalProps) {
  const preview = usePreviewOrderImport();
  const [file, setFile] = useState<File | null>(null);
  const data = file ? preview.data : undefined;

  return (
    <Modal
      open
      width={MODAL_WIDTH.xl}
      title={text.title}
      okText={text.apply}
      cancelText={MESSAGES.common.cancel}
      okButtonProps={{ disabled: !data || data.validCount === 0 || preview.isPending }}
      onOk={() => data && onApply(data.rows)}
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
              {fill(text.summary, data.validCount, data.errorCount, data.duplicateCount)}
            </Typography.Text>
            <DataTable<OrderImportRow>
              rowKey="rowNumber"
              size="small"
              columns={columnsFor(data.sizeColumns)}
              dataSource={data.rows}
              scroll={{ x: 'max-content', y: 360 }}
            />
          </>
        )}
      </Space>
    </Modal>
  );
}
