import { Card } from 'antd';
import { DataTable } from '@/components/common/DataTable';
import { MESSAGES } from '@/constants/messages';
import { formatCurrency, formatQty } from '@/utils/format';
import { blockTotal, fromLines, rowTotal } from '../utils/orderGrid';
import type { OrderDisplayLine } from '../types';
import { BlockSummaryRow } from './BlockSummaryRow';

/** Lưới số lượng của đơn (màu × size) theo từng mã hàng, chỉ đọc; có tổng theo màu và theo size. Dòng không có giá (Quản đốc) thì không hiện tiền. */
export function OrderReadOnlyGrid({ lines }: { lines: OrderDisplayLine[] }) {
  return (
    <>
      {fromLines(lines).map((block) => {
        const first = lines.find((l) => l.productId === block.productId);
        const total = blockTotal(block);
        return (
          <Card
            key={block.key}
            size="small"
            style={{ marginBottom: 12 }}
            title={`${first?.productCode ?? ''} · ${first?.productName ?? ''}`}
            extra={
              block.cmtUnitPrice !== null &&
              `${MESSAGES.orders.unitPrice}: ${formatCurrency(block.cmtUnitPrice)} · ${MESSAGES.orders.totalAmount}: ${formatCurrency(total * block.cmtUnitPrice)}`
            }
          >
            <DataTable
              rowKey="key"
              pagination={false}
              dataSource={block.rows}
              columns={[
                { title: MESSAGES.orders.color, dataIndex: 'color' },
                ...block.sizes.map((size) => ({
                  title: size,
                  align: 'right' as const,
                  render: (_: unknown, row: (typeof block.rows)[number]) =>
                    formatQty(row.cells[size]?.qty ?? null),
                })),
                {
                  title: MESSAGES.orders.totalQty,
                  align: 'right' as const,
                  render: (_: unknown, row: (typeof block.rows)[number]) =>
                    formatQty(rowTotal(block, row)),
                },
              ]}
              summary={() =>
                block.rows.length > 1 ? <BlockSummaryRow block={block} /> : undefined
              }
            />
          </Card>
        );
      })}
    </>
  );
}
