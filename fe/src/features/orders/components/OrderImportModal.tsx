import { useRef, useState } from 'react';
import { Alert, Button, Modal, Space, Tag, Typography, Upload } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { InboxOutlined, PlusOutlined } from '@ant-design/icons';
import { DataTable } from '@/components/common/DataTable';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { MESSAGES } from '@/constants/messages';
import { fill, formatCurrency, formatQty } from '@/utils/format';
import { ReasonHover } from '@/components/common/ReasonHover';
import { ImportTemplateLink } from '@/components/common/ImportTemplateLink';
import { unknownCodesOf, useCreateProductFromImport } from '../hooks/useCreateProductFromImport';
import { usePreviewOrderImport } from '../hooks/useSpecImport';
import type { OrderImportRow, SaveProductPayload } from '../types';
import { acceptMissingProduct } from '../utils/orderImport';
import type { ProductImportSelection } from '../utils/productExcelImports';
import { ProductFormModal } from './ProductFormModal';

const text = MESSAGES.orders.orderImport;
const col = text.columns;

type OrderImportModalProps = {
  /** Đơn đang sửa (null / bỏ trống khi tạo mới): file mẫu chứa dòng hàng hiện có của đơn. */
  orderId?: number | null;
  onApply: (rows: OrderImportRow[]) => void;
  onClose: () => void;
  /**
   * DEV-ALL-151: khai mã chưa có (chưa ghi). File Excel của mã được giao cho form đơn, nhập sau khi Lưu đơn.
   * Không truyền thì không hiện nút tạo.
   */
  onStageProduct?: (body: SaveProductPayload, selection: ProductImportSelection) => number | null;
  onUnstageProduct?: (tempId: number) => void;
};

const columnsFor = (sizes: string[]): ColumnsType<OrderImportRow> => [
  { title: col.row, dataIndex: 'rowNumber', width: 64 },
  {
    title: col.product,
    key: 'product',
    dataIndex: 'productCode',
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
      <ReasonHover reasons={[r.error && { text: r.error, type: 'danger' }]}>
        <Tag color={r.error ? text.invalid.color : text.valid.color}>
          {r.error ? text.invalid.text : text.valid.text}
        </Tag>
      </ReasonHover>
    ),
  },
];

/**
 * DEV-ALL-118: nhập dòng hàng của đơn từ Excel dạng ma trận màu × size. Chọn file là xem trước (máy chủ kiểm mã hàng, số lượng,
 * đơn giá); Áp dụng thì điền các dòng hợp lệ vào form, người dùng xem lại và bấm Lưu như tạo tay. Chỉ gắn khi đang mở.
 */
export function OrderImportModal({
  orderId,
  onApply,
  onClose,
  onStageProduct,
  onUnstageProduct,
}: OrderImportModalProps) {
  const preview = usePreviewOrderImport();
  const [file, setFile] = useState<File | null>(null);
  const [accepted, setAccepted] = useState<Record<string, number>>({});
  const stagedIds = useRef<number[]>([]);
  const create = useCreateProductFromImport(file);
  const creating = create.creating;
  const raw = file ? preview.data : undefined;
  const data = raw
    ? Object.entries(accepted).reduce(
        (previewRows, [code, tempId]) => acceptMissingProduct(previewRows, code, tempId),
        raw,
      )
    : undefined;
  const unknownCodes = data && onStageProduct ? unknownCodesOf(data.rows) : [];
  const dropStaged = () => {
    stagedIds.current.forEach((id) => onUnstageProduct?.(id));
    stagedIds.current = [];
    setAccepted({});
  };

  return (
    <Modal
      open
      width={MODAL_WIDTH.xl}
      title={text.title}
      okText={text.apply}
      cancelText={MESSAGES.common.cancel}
      okButtonProps={{ disabled: !data || data.validCount === 0 || preview.isPending }}
      onOk={() => {
        if (!data) return;
        stagedIds.current = [];
        onApply(data.rows);
      }}
      onCancel={() => {
        dropStaged();
        onClose();
      }}
      destroyOnHidden
    >
      <Space direction="vertical" size={16} style={{ width: '100%' }}>
        <Typography.Text type="secondary">{text.hint}</Typography.Text>
        <ImportTemplateLink kind="order-lines" orderId={orderId} />
        <Upload.Dragger
          maxCount={1}
          accept=".xlsx"
          disabled={preview.isPending}
          beforeUpload={(next) => {
            dropStaged();
            setFile(next);
            preview.mutate(next);
            return false;
          }}
          onRemove={() => {
            dropStaged();
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
        {unknownCodes.length > 0 && (
          <Alert
            type="warning"
            showIcon
            message={text.unknownProducts}
            description={
              <Space wrap>
                {unknownCodes.map((code) => (
                  <Button
                    key={code}
                    size="small"
                    type="primary"
                    icon={<PlusOutlined />}
                    loading={create.starting}
                    onClick={() => create.start(code)}
                  >
                    {fill(text.createProduct, code)}
                  </Button>
                ))}
              </Space>
            }
          />
        )}
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
      {/* Gắn riêng mỗi lần tạo để form nhận file gán sẵn. Lưu form không ghi mã: dòng chỉ thiếu mã được nhận, mã ghi khi Lưu đơn. */}
      {creating && file && onStageProduct && (
        <ProductFormModal
          open
          product={null}
          allowImports
          initialValues={creating.values}
          initialImports={creating.imports}
          onStage={(body, selection) => {
            const tempId = onStageProduct(body, selection);
            if (tempId === null) return false;
            stagedIds.current = [...stagedIds.current, tempId];
            setAccepted((prev) => ({ ...prev, [body.productCode.trim().toUpperCase()]: tempId }));
            return true;
          }}
          onClose={create.stop}
        />
      )}
    </Modal>
  );
}
