import { useState } from 'react';
import { Button } from 'antd';
import { UploadOutlined } from '@ant-design/icons';
import { ModalActions } from '@/components/common/DetailModal';
import { MESSAGES } from '@/constants/messages';
import { confirmDialog } from '@/utils/confirmDialog';
import type { ProductListItem } from '../types';
import { SpecImportModal } from './SpecImportModal';

const text = MESSAGES.orders.specImport;

type SpecEditorActionsProps = {
  product: ProductListItem;
  readOnly: boolean;
  /** Form định mức đang có thay đổi chưa lưu: hỏi trước khi mở nhập Excel (nhập lưu theo bản trên máy chủ). */
  dirty: boolean;
  saving: boolean;
  onSave: () => void;
  onClose: () => void;
};

/** Nút của form định mức: Đóng, Nhập từ Excel (DEV-ALL-115, chỉ người sửa được định mức), Lưu. */
export function SpecEditorActions({
  product,
  readOnly,
  dirty,
  saving,
  onSave,
  onClose,
}: SpecEditorActionsProps) {
  const [importing, setImporting] = useState(false);

  const openImport = () => {
    if (!dirty) return setImporting(true);
    confirmDialog({
      title: text.button,
      content: text.unsavedFirst,
      onOk: () => setImporting(true),
    });
  };

  return (
    <>
      <ModalActions>
        <Button onClick={onClose}>{MESSAGES.common.close}</Button>
        {!readOnly && (
          <Button icon={<UploadOutlined />} onClick={openImport}>
            {text.button}
          </Button>
        )}
        {!readOnly && (
          <Button type="primary" loading={saving} onClick={onSave}>
            {MESSAGES.common.save}
          </Button>
        )}
      </ModalActions>
      {importing && (
        <SpecImportModal
          source={{
            scope: 'admin',
            productId: product.productId,
            productLabel: `${product.productCode}`,
          }}
          onClose={() => setImporting(false)}
        />
      )}
    </>
  );
}
