import { Typography } from 'antd';
import { DataTable } from '@/components/common/DataTable';
import { MESSAGES } from '@/constants/messages';
import { formatQty } from '@/utils/format';
import { blockTotal, sizeTotal, type ProductBlock } from '../utils/orderGrid';

type BlockSummaryRowProps = {
  block: ProductBlock;
  /** Bảng có thêm cột nút xóa ở cuối. */
  hasActionColumn?: boolean;
};

/** Dòng tổng của lưới màu × size: tổng từng size và tổng cả mã hàng. */
export function BlockSummaryRow({ block, hasActionColumn }: BlockSummaryRowProps) {
  const totalIndex = block.sizes.length + 1;
  return (
    <DataTable.Summary.Row>
      <DataTable.Summary.Cell index={0}>
        <Typography.Text strong>{MESSAGES.orders.totalQty}</Typography.Text>
      </DataTable.Summary.Cell>
      {block.sizes.map((size, i) => (
        <DataTable.Summary.Cell key={size} index={i + 1} align="right">
          {formatQty(sizeTotal(block, size))}
        </DataTable.Summary.Cell>
      ))}
      <DataTable.Summary.Cell index={totalIndex} align="right">
        <Typography.Text strong>{formatQty(blockTotal(block))}</Typography.Text>
      </DataTable.Summary.Cell>
      {hasActionColumn && <DataTable.Summary.Cell index={totalIndex + 1} />}
    </DataTable.Summary.Row>
  );
}
