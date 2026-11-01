import { useEffect, useRef, useState } from 'react';
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons';
import { Button, Flex, Typography, Upload, message } from 'antd';
import { IconAction } from '@/components/common/IconAction';
import { MESSAGES } from '@/constants/messages';
import type { ApiError } from '@/types/api';
import { fill } from '@/utils/format';
import { productApi } from '../api/orderApi';
import { MAX_EXTRA_IMAGES } from '../constants';
import { isAcceptableImage } from '../utils/imageFile';
import { ProductImage } from './ProductImage';

const text = MESSAGES.orders;
const THUMB = 80;

type Props = { value?: string[]; onChange?: (value: string[]) => void };

/** Ảnh phụ của mã hàng (tối đa 5): chọn được nhiều ảnh một lần, tải lên ngay khi chọn, giá trị form là các khóa ảnh trả về từ máy chủ. */
export function ExtraImagesInput({ value = [], onChange }: Props) {
  const [uploading, setUploading] = useState(0);
  // Chọn nhiều ảnh cùng lúc thì các lần tải xong chồng lên nhau: giữ danh sách mới nhất và số chỗ đã giữ cho ảnh đang tải.
  const latest = useRef(value);
  const reserved = useRef(0);
  useEffect(() => {
    latest.current = value;
  }, [value]);

  const beforeUpload = (file: File) => {
    if (!isAcceptableImage(file)) {
      void message.error(text.imageHint);
      return Upload.LIST_IGNORE;
    }
    if (latest.current.length + reserved.current >= MAX_EXTRA_IMAGES) {
      void message.warning(fill(text.extraImagesFull, MAX_EXTRA_IMAGES));
      return Upload.LIST_IGNORE;
    }
    reserved.current += 1;
    setUploading((n) => n + 1);
    productApi
      .uploadSampleImage(file)
      .then((key) => {
        latest.current = [...latest.current, key];
        onChange?.(latest.current);
      })
      .catch((e: ApiError) => void message.error(e.message))
      .finally(() => {
        reserved.current -= 1;
        setUploading((n) => n - 1);
      });
    return false;
  };

  const full = value.length >= MAX_EXTRA_IMAGES;

  return (
    <Flex vertical gap={8}>
      <Flex wrap gap={8}>
        {value.map((key) => (
          <div key={key} style={{ position: 'relative', width: THUMB, height: THUMB }}>
            <ProductImage value={key} size={THUMB} />
            <div
              style={{
                position: 'absolute',
                top: 0,
                right: 0,
                background: 'rgba(255,255,255,0.85)',
              }}
            >
              <IconAction
                danger
                title={text.removeImage}
                icon={<DeleteOutlined />}
                onClick={() => {
                  latest.current = value.filter((k) => k !== key);
                  onChange?.(latest.current);
                }}
              />
            </div>
          </div>
        ))}
        {!full && (
          <Upload
            multiple
            accept="image/png,image/jpeg,image/webp"
            showUploadList={false}
            beforeUpload={beforeUpload}
          >
            <Button type="dashed" loading={uploading > 0} style={{ width: THUMB, height: THUMB }}>
              <div>
                <PlusOutlined />
                <div>{text.addExtraImage}</div>
              </div>
            </Button>
          </Upload>
        )}
      </Flex>
      <Typography.Text type="secondary">
        {fill(text.extraImagesCount, value.length, MAX_EXTRA_IMAGES)}
      </Typography.Text>
    </Flex>
  );
}
