import { useState } from 'react';
import { Button, Space, Typography, Upload, message } from 'antd';
import { MESSAGES } from '@/constants/messages';
import type { ApiError } from '@/types/api';
import { productApi } from '../api/orderApi';
import { ProductImage } from './ProductImage';

const MAX_BYTES = 5 * 1024 * 1024;

type Props = { value?: string; onChange?: (value: string | undefined) => void };

/** Chọn ảnh mẫu từ máy: tải lên ngay khi chọn, giá trị form là khóa ảnh trả về từ máy chủ. */
export function SampleImageInput({ value, onChange }: Props) {
  const [uploading, setUploading] = useState(false);

  const beforeUpload = (file: File) => {
    if (!file.type.startsWith('image/') || file.size > MAX_BYTES) {
      void message.error(MESSAGES.orders.imageHint);
      return Upload.LIST_IGNORE;
    }
    setUploading(true);
    productApi
      .uploadSampleImage(file)
      .then((key) => onChange?.(key))
      .catch((e: ApiError) => void message.error(e.message))
      .finally(() => setUploading(false));
    return false;
  };

  return (
    <Space direction="vertical">
      {value && <ProductImage value={value} size={96} />}
      <Space>
        <Upload accept="image/png,image/jpeg,image/webp" showUploadList={false} beforeUpload={beforeUpload}>
          <Button loading={uploading}>{value ? MESSAGES.orders.changeImage : MESSAGES.orders.chooseImage}</Button>
        </Upload>
        {value && <Button danger onClick={() => onChange?.(undefined)}>{MESSAGES.orders.removeImage}</Button>}
      </Space>
      <Typography.Text type="secondary">{MESSAGES.orders.imageHint}</Typography.Text>
    </Space>
  );
}
