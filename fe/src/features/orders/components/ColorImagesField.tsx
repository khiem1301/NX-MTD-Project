import { useMemo } from 'react';
import { PlusOutlined } from '@ant-design/icons';
import { Button, Flex, Form, Typography } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { fill } from '@/utils/format';
import { MAX_COLOR_IMAGES, MAX_IMAGE_COLORS, MIN_COLOR_IMAGES } from '../constants';
import type { ProductColorImages, SaveProductPayload } from '../types';
import { ColorImagesRow } from './ColorImagesRow';

const text = MESSAGES.orders;
const NEW_ROW: ProductColorImages = { color: '', images: [] };

type ColorImagesFieldProps = {
  /** Màu đã có của mã hàng, gợi ý trước các màu khác. */
  suggestions?: readonly string[];
};

/** Ảnh theo màu áo của mã hàng (DEV-ALL-71): mỗi màu một khung, 1-3 ảnh. Đặt trong Form của ProductFormModal. */
export function ColorImagesField({ suggestions = [] }: ColorImagesFieldProps) {
  const rows = Form.useWatch<SaveProductPayload['colorImages']>('colorImages');
  const colors = useMemo(
    () => [...suggestions, ...(rows ?? []).map((r) => r?.color ?? '')],
    [suggestions, rows],
  );

  return (
    <Form.Item
      label={text.colorImages}
      tooltip={fill(text.colorImagesHint, MIN_COLOR_IMAGES, MAX_COLOR_IMAGES)}
      style={{ marginBottom: 0 }}
    >
      <Form.List name="colorImages">
        {(fields, { add, remove }) => (
          <Flex vertical gap={12}>
            {fields.length === 0 && (
              <Typography.Text type="secondary">{text.noColorImages}</Typography.Text>
            )}
            {fields.map((field) => (
              <ColorImagesRow
                key={field.key}
                name={field.name}
                suggestions={colors}
                onRemove={() => remove(field.name)}
              />
            ))}
            {fields.length < MAX_IMAGE_COLORS && (
              <Button type="dashed" icon={<PlusOutlined />} onClick={() => add(NEW_ROW)} block>
                {text.addColor}
              </Button>
            )}
          </Flex>
        )}
      </Form.List>
    </Form.Item>
  );
}
