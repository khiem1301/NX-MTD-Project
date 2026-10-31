import type { Dispatch, SetStateAction } from 'react';
import { Alert, InputNumber, Typography } from 'antd';
import { DataTable } from '@/components/common/DataTable';
import { MESSAGES } from '@/constants/messages';
import { patchRow, type VariantRow } from '../utils/productSpec';

const text = MESSAGES.orders.spec;

type SpecVariantsTabProps = {
  variants: VariantRow[];
  setVariants: Dispatch<SetStateAction<VariantRow[]>>;
  /** Màu – size đã ngừng dùng (đã có đơn hàng), chỉ để báo. */
  discontinuedVariants: string[];
};

/** Tab "Màu – size": lấy từ đơn hàng (chỉ đọc), chỉ nhập kích thước thành phẩm theo từng màu – size nếu cần. */
export function SpecVariantsTab({
  variants,
  setVariants,
  discontinuedVariants,
}: SpecVariantsTabProps) {
  return (
    <>
      <Typography.Paragraph type="secondary">{text.variantHint}</Typography.Paragraph>
      <DataTable<VariantRow>
        rowKey="key"
        size="small"
        pagination={{ pageSize: 10, showSizeChanger: false, hideOnSinglePage: true }}
        dataSource={variants}
        columns={[
          { title: text.color, dataIndex: 'color' },
          { title: text.size, dataIndex: 'sizeCode', width: 120 },
          {
            title: text.variantLength,
            width: 190,
            render: (_, r) => (
              <InputNumber
                size="small"
                min={0}
                precision={2}
                value={r.lengthCm}
                style={{ width: 150 }}
                onChange={(v) => patchRow(setVariants, r.key, { lengthCm: v })}
              />
            ),
          },
          {
            title: text.variantWidth,
            width: 190,
            render: (_, r) => (
              <InputNumber
                size="small"
                min={0}
                precision={2}
                value={r.widthCm}
                style={{ width: 150 }}
                onChange={(v) => patchRow(setVariants, r.key, { widthCm: v })}
              />
            ),
          },
        ]}
      />
      {discontinuedVariants.length > 0 && (
        <Alert
          style={{ marginTop: 12 }}
          type="info"
          showIcon
          message={`${text.discontinuedVariants}: ${discontinuedVariants.join(', ')}`}
        />
      )}
    </>
  );
}
