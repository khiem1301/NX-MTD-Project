import { useState } from 'react';
import { Button, Space, Upload, message } from 'antd';
import { MESSAGES } from '@/constants/messages';
import type { ApiError } from '@/types/api';
import { productApi } from '../api/orderApi';
import { isAcceptableImage } from '../utils/imageFile';
import { ProductImage } from './ProductImage';
import { HintTip } from '@/components/common/HintTip';

type Props = {
  value?: string;
  onChange?: (value: string | undefined) => void;
  /** Ẩn dòng gợi ý định dạng (nơi dùng tự ghi một dòng chung cho mọi ô ảnh). */
  hideHint?: boolean;
  /** Cỡ ảnh xem trước (px). */
  size?: number;
};

const DEFAULT_SIZE = 96;

/** Chọn ảnh mẫu từ máy: tải lên ngay khi chọn, giá trị form là khóa ảnh trả về từ máy chủ. */
export function SampleImageInput({ value, onChange, hideHint, size = DEFAULT_SIZE }: Props) {
  const [uploading, setUploading] = useState(false);

  const beforeUpload = (file: File) => {
    if (!isAcceptableImage(file)) {
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
      {value && <ProductImage value={value} size={size} />}
      <Space>
        <Upload
          accept="image/png,image/jpeg,image/webp"
          showUploadList={false}
          beforeUpload={beforeUpload}
        >
          <Button loading={uploading}>
            {value ? MESSAGES.orders.changeImage : MESSAGES.orders.chooseImage}
          </Button>
        </Upload>
        {value && (
          <Button danger onClick={() => onChange?.(undefined)}>
            {MESSAGES.orders.removeImage}
          </Button>
        )}
      </Space>
      {!hideHint && <HintTip text={MESSAGES.orders.imageHint} />}
    </Space>
  );
}
