import type { ReactNode } from 'react';
import { Button, Upload } from 'antd';
import { UploadOutlined } from '@ant-design/icons';

const EXCEL_ACCEPT = '.xlsx';

type ExcelUploadButtonProps = {
  disabled?: boolean;
  loading?: boolean;
  /** Nút nổi bật (viền màu chính) cho "Tải lên 1 file". */
  primary?: boolean;
  onFile: (file: File) => void;
  children: ReactNode;
};

/** DEV-ALL-121/122: nút chọn một file Excel (.xlsx); không tải lên ngay, giao file cho nơi gọi đọc và mở popup kiểm tra. */
export function ExcelUploadButton({
  disabled,
  loading,
  primary,
  onFile,
  children,
}: ExcelUploadButtonProps) {
  return (
    <Upload
      accept={EXCEL_ACCEPT}
      maxCount={1}
      showUploadList={false}
      disabled={disabled || loading}
      beforeUpload={(file) => {
        onFile(file);
        return false;
      }}
    >
      <Button
        size="small"
        type={primary ? 'primary' : 'default'}
        ghost={primary}
        icon={<UploadOutlined />}
        loading={loading}
        disabled={disabled}
      >
        {children}
      </Button>
    </Upload>
  );
}
