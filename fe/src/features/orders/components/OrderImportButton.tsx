import { useState } from 'react';
import { Button, message } from 'antd';
import { UploadOutlined } from '@ant-design/icons';
import { MESSAGES } from '@/constants/messages';
import { fill } from '@/utils/format';
import type { ProductBlock } from '../utils/orderGrid';
import type { ProductImportSelection } from '../utils/productExcelImports';
import { mergeOrderImport } from '../utils/orderImport';
import type { SaveProductPayload } from '../types';
import { OrderImportModal } from './OrderImportModal';

const text = MESSAGES.orders.orderImport;

type OrderImportButtonProps = {
  orderId?: number | null;
  blocks: ProductBlock[];
  onApply: (blocks: ProductBlock[], labels: Record<number, string>) => void;
  /** DEV-ALL-151: khai mã chưa có, chưa ghi. Trả tempId âm; null khi mã đã có trên đơn. */
  onStageProduct?: (
    body: SaveProductPayload,
    selection: ProductImportSelection,
  ) => number | null;
  /** Đóng hộp nhập mà không Áp dụng thì bỏ các mã vừa khai trong hộp. */
  onUnstageProduct?: (tempId: number) => void;
};

/** DEV-ALL-118: nút "Nhập từ Excel" của form đơn; áp dụng thì gộp dòng hợp lệ vào các khối mã hàng (chưa lưu). */
export function OrderImportButton({
  orderId,
  blocks,
  onApply,
  onStageProduct,
  onUnstageProduct,
}: OrderImportButtonProps) {
  const [open, setOpen] = useState(false);
  return (
    <>
      <Button icon={<UploadOutlined />} onClick={() => setOpen(true)}>
        {text.button}
      </Button>
      {open && (
        <OrderImportModal
          orderId={orderId}
          onClose={() => setOpen(false)}
          onStageProduct={onStageProduct}
          onUnstageProduct={onUnstageProduct}
          onApply={(rows) => {
            const merged = mergeOrderImport(blocks, rows);
            onApply(merged.blocks, merged.labels);
            void message.success(fill(text.applied, rows.filter((r) => r.error === null).length));
            setOpen(false);
          }}
        />
      )}
    </>
  );
}
