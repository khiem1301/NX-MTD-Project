import type { ReactNode } from 'react';
import { Button, Space, Tag, Upload } from 'antd';
import { FileExcelOutlined, UploadOutlined } from '@ant-design/icons';

type Props = {
  /** Tên file đang xem; chưa chọn thì hiện lời nhắc. */
  fileName?: string;
  emptyText: string;
  hint?: string;
  loading?: boolean;
  /** Gọi khi chọn file; file không được tải lên ngay mà do nơi dùng quyết định. */
  onPick: (file: File) => void;
  /** Phần phụ nằm cùng hàng (liên kết file mẫu...). */
  extra?: ReactNode;
};

/**
 * Thanh chọn file Excel gọn dùng chung cho các popup nhập Excel: một hàng gồm nút chọn file, tên file đang xem và phần phụ.
 * Thay khung kéo thả cao để bảng xem trước có nhiều chỗ hơn; chọn lại file bằng cùng nút.
 */
export function ImportFileBar({ fileName, emptyText, hint, loading, onPick, extra }: Props) {
  return (
    <Space direction="vertical" size={4} style={{ width: '100%' }}>
      <Space wrap size={12}>
        <Upload
          accept=".xlsx"
          showUploadList={false}
          disabled={loading}
          beforeUpload={(file) => {
            onPick(file);
            return false;
          }}
        >
          <Button
            type={fileName ? 'default' : 'primary'}
            icon={<UploadOutlined />}
            loading={loading}
          >
            {fileName ? 'Chọn file khác' : emptyText}
          </Button>
        </Upload>
        {fileName && (
          <Tag icon={<FileExcelOutlined />} color="green">
            {fileName}
          </Tag>
        )}
        {extra}
      </Space>
      {hint && !fileName && (
        <span style={{ color: 'var(--ant-color-text-secondary)' }}>{hint}</span>
      )}
    </Space>
  );
}
