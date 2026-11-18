import { useState } from 'react';
import { Button, message } from 'antd';
import { UploadOutlined } from '@ant-design/icons';
import { MESSAGES } from '@/constants/messages';
import { fill } from '@/utils/format';
import type { ProductBlock } from '../utils/orderGrid';
import { mergeOrderImport } from '../utils/orderImport';
import { OrderImportModal } from './OrderImportModal';

const text = MESSAGES.orders.orderImport;

type OrderImportButtonProps = {
  blocks: ProductBlock[];
  onApply: (blocks: ProductBlock[], labels: Record<number, string>) => void;
};

/** DEV-ALL-118: nút "Nhập từ Excel" của form đơn; áp dụng thì gộp dòng hợp lệ vào các khối mã hàng (chưa lưu). */
export function OrderImportButton({ blocks, onApply }: OrderImportButtonProps) {
  const [open, setOpen] = useState(false);
  return (
    <>
      <Button icon={<UploadOutlined />} onClick={() => setOpen(true)}>
        {text.button}
      </Button>
      {open && (
        <OrderImportModal
          onClose={() => setOpen(false)}
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
