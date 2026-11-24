import { useRef, useState } from 'react';
import { Alert, Button, Input, InputNumber, Modal, Select, Space, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons';
import { HintTip } from '@/components/common/HintTip';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { ReasonHover } from '@/components/common/ReasonHover';
import { SIZE_ORDER } from '@/constants/orderStatus';
import { MESSAGES } from '@/constants/messages';
import { fill } from '@/utils/format';
import { unknownCodesOf, useCreateProductFromImport } from '../hooks/useCreateProductFromImport';
import { useOrderImportDraft } from '../hooks/useOrderImportDraft';
import type { OrderImportPreview, OrderImportRow, SaveProductPayload } from '../types';
import { acceptStagedProducts } from '../utils/orderImport';
import type { ProductImportSelection } from '../utils/productExcelImports';
import { CmtPriceInput } from './CmtPriceInput';
import { ExcelReviewTable } from './ExcelReviewTable';
import { ProductFormModal } from './ProductFormModal';

const text = MESSAGES.orders.orderImport;
const col = text.columns;
const SIZE_OPTIONS = SIZE_ORDER.map((s) => ({ value: s, label: s }));

type OrderImportReviewModalProps = {
  file: File;
  /** Kết quả xem trước của file: bản nháp sửa được bắt đầu từ đây. */
  initial: OrderImportPreview;
  /** Điền các dòng hợp lệ (đã kiểm) vào form. */
  onApply: (rows: OrderImportRow[]) => void;
  /** Quay lại hộp chọn file (bỏ bản sửa). */
  onBack: () => void;
  onStageProduct?: (body: SaveProductPayload, selection: ProductImportSelection) => number | null;
  onUnstageProduct?: (tempId: number) => void;
  stagedProducts?: Record<string, number>;
};

/**
 * DEV-ALL-178: popup kiểm tra dòng hàng đọc từ Excel — sửa trực tiếp từng ô (mã hàng, màu, đơn giá CMT, số lượng từng size), thêm /
 * xóa dòng, thêm cột size; máy chủ kiểm lại sau mỗi lần sửa. Áp dụng thì điền các dòng hợp lệ vào form đơn (chưa lưu).
 */
export function OrderImportReviewModal({
  file,
  initial,
  onApply,
  onBack,
  onStageProduct,
  onUnstageProduct,
  stagedProducts = {},
}: OrderImportReviewModalProps) {
  const draft = useOrderImportDraft(initial);
  const [accepted, setAccepted] = useState<Record<string, number>>({});
  const stagedIds = useRef<number[]>([]);
  const create = useCreateProductFromImport(file);
  const data = acceptStagedProducts(draft.result, { ...stagedProducts, ...accepted });
  const unknownCodes = onStageProduct ? unknownCodesOf(data.rows) : [];
  const statusOf = (rowNumber: number) => data.rows.find((r) => r.rowNumber === rowNumber);
  const dropStaged = () => {
    stagedIds.current.forEach((id) => onUnstageProduct?.(id));
    stagedIds.current = [];
    setAccepted({});
  };

  const columns: ColumnsType<OrderImportRow> = [
    { title: col.row, dataIndex: 'rowNumber', width: 56 },
    {
      title: col.product,
      key: 'product',
      width: 150,
      render: (_, r) => (
        <Input
          value={r.productCode}
          onChange={(e) => draft.update(r.rowNumber, { productCode: e.target.value.toUpperCase() })}
        />
      ),
    },
    {
      title: col.color,
      key: 'color',
      width: 120,
      render: (_, r) => (
        <Input
          value={r.color}
          onChange={(e) => draft.update(r.rowNumber, { color: e.target.value })}
        />
      ),
    },
    {
      title: col.price,
      key: 'price',
      width: 150,
      render: (_, r) => (
        <CmtPriceInput
          value={r.cmtUnitPrice}
          onChange={(v) => draft.update(r.rowNumber, { cmtUnitPrice: v })}
        />
      ),
    },
    ...draft.sizes.map((size) => ({
      title: size,
      key: `size-${size}`,
      width: 84,
      render: (_: unknown, r: OrderImportRow) => (
        <InputNumber
          style={{ width: '100%' }}
          min={0}
          precision={0}
          value={r.quantities.find((q) => q.sizeCode === size)?.qty ?? 0}
          onChange={(v) => draft.setQty(r.rowNumber, size, v)}
        />
      ),
    })),
    {
      title: col.result,
      key: 'result',
      width: 260,
      fixed: 'right' as const,
      render: (_, r) => {
        const error = statusOf(r.rowNumber)?.error ?? null;
        return (
          <ReasonHover reasons={[error && { text: error, type: 'danger' }]}>
            <Tag color={error ? text.invalid.color : text.valid.color}>
              {error ? text.invalid.text : text.valid.text}
            </Tag>
          </ReasonHover>
        );
      },
    },
    {
      key: 'remove',
      width: 48,
      fixed: 'right' as const,
      render: (_, r) => (
        <Button
          type="text"
          danger
          size="small"
          aria-label={MESSAGES.common.delete}
          icon={<DeleteOutlined />}
          onClick={() => draft.remove(r.rowNumber)}
        />
      ),
    },
  ];

  const close = () => {
    dropStaged();
    onBack();
  };

  return (
    <Modal
      open
      width={MODAL_WIDTH.xl}
      title={fill(text.reviewTitle, file.name)}
      okText={text.apply}
      cancelText={MESSAGES.common.cancel}
      okButtonProps={{ disabled: data.validCount === 0 || draft.checking }}
      onOk={() => {
        stagedIds.current = [];
        onApply(data.rows);
      }}
      onCancel={close}
      destroyOnHidden
      maskClosable={false}
    >
      <Space direction="vertical" size={12} style={{ width: '100%' }}>
        <HintTip text={text.reviewHint} />
        {draft.checkError && <Alert type="error" showIcon message={draft.checkError} />}
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
        <Space wrap size={16}>
          <Typography.Text>
            {draft.checking
              ? text.checking
              : fill(text.summary, data.validCount, data.errorCount, data.duplicateCount)}
          </Typography.Text>
          <Select
            showSearch
            placeholder={text.addSize}
            style={{ width: 130 }}
            value={null}
            options={SIZE_OPTIONS.filter((o) => !draft.sizes.includes(o.value))}
            onSearch={() => undefined}
            onChange={(v: string) => draft.addSize(v)}
            notFoundContent={null}
          />
        </Space>
        <ExcelReviewTable<OrderImportRow>
          rows={draft.rows}
          columns={columns}
          stateOf={(r) => (statusOf(r.rowNumber)?.error ? 'error' : 'valid')}
          onlyErrors={false}
          onAdd={draft.add}
        />
      </Space>
      {/* Gắn riêng mỗi lần tạo để form nhận file gán sẵn. Lưu form không ghi mã: dòng chỉ thiếu mã được nhận, mã ghi khi Lưu đơn. */}
      {create.creating && onStageProduct && (
        <ProductFormModal
          open
          product={null}
          allowImports
          initialValues={create.creating.values}
          initialImports={create.creating.imports}
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
