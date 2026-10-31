import { Card } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { formatCurrency, formatQty } from '@/utils/format';
import { blockTotal, fromLines } from '../utils/orderGrid';
import type { OrderLine } from '../types';
import { DataTable } from '@/components/common/DataTable';

/** Lưới số lượng của đơn (màu × size) theo từng mã hàng, chỉ đọc. */
export function OrderReadOnlyGrid({ lines }: { lines: OrderLine[] }) {
  return (
    <>
      {fromLines(lines).map((block) => {
        const first = lines.find((l) => l.productId === block.productId);
        return (
          <Card
            key={block.key}
            size="small"
            style={{ marginBottom: 12 }}
            title={`${first?.productCode ?? ''} · ${first?.productName ?? ''}`}
            extra={`${MESSAGES.orders.unitPrice}: ${formatCurrency(block.cmtUnitPrice)} · ${MESSAGES.orders.totalQty}: ${formatQty(blockTotal(block))}`}
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
              ]}
            />
          </Card>
        );
      })}
    </>
  );
}
