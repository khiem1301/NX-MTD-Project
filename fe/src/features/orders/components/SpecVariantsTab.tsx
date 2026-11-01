import { Alert, Typography } from 'antd';
import { DataTable } from '@/components/common/DataTable';
import { MESSAGES } from '@/constants/messages';
import type { VariantRow } from '../utils/productSpec';

const text = MESSAGES.orders.spec;

type SpecVariantsTabProps = {
  variants: VariantRow[];
  /** Màu – size đã ngừng dùng (đã có đơn hàng), chỉ để báo. */
  discontinuedVariants: string[];
};

/** Tab "Màu – size": lấy từ đơn hàng, chỉ đọc. */
export function SpecVariantsTab({ variants, discontinuedVariants }: SpecVariantsTabProps) {
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
