import { Alert, Card, Descriptions, Table, Tag } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { MESSAGES } from '@/constants/messages';
import { formatDateTime } from '@/utils/format';
import { MONTHLY_COST_CATEGORY_LABEL, MONTHLY_COST_TEXT, MONTHLY_COST_TYPE_LABEL } from '../constants';
import type { MonthlyCostItem, MonthlyCostSummary } from '../types';
import { formatCurrency } from '../utils';

type Props = { summary: MonthlyCostSummary };

const columns: ColumnsType<MonthlyCostItem> = [
  {
    title: MONTHLY_COST_TEXT.table.category,
    dataIndex: 'costCategoryCode',
    render: (v: MonthlyCostItem['costCategoryCode']) => MONTHLY_COST_CATEGORY_LABEL[v],
  },
  {
    title: MONTHLY_COST_TEXT.table.type,
    dataIndex: 'costType',
    render: (v: MonthlyCostItem['costType']) => (
      <Tag color={MONTHLY_COST_TYPE_LABEL[v].color}>{MONTHLY_COST_TYPE_LABEL[v].text}</Tag>
    ),
  },
  {
    title: MONTHLY_COST_TEXT.table.product,
    dataIndex: 'productCode',
    render: (v: string | null, row) => (v ? `${v} — ${row.productName ?? ''}` : '—'),
  },
  {
    title: MONTHLY_COST_TEXT.table.amount,
    dataIndex: 'amount',
    align: 'right',
    render: (v: number) => formatCurrency(v),
  },
  {
    title: MONTHLY_COST_TEXT.table.quantity,
    dataIndex: 'quantity',
    align: 'right',
    render: (v: number | null) => v ?? '—',
  },
  { title: MONTHLY_COST_TEXT.table.note, dataIndex: 'note', render: (v: string | null) => v ?? '—' },
  {
    title: MONTHLY_COST_TEXT.table.importedAt,
    dataIndex: 'importedAt',
    render: (v: string) => formatDateTime(v),
  },
];

/** UC12/UC33 — chi phí đã nhập của tháng hiện tại, kèm đối soát MEALS với Σ DailyMeal.TotalAmount. */
export function MonthlyCostSummaryPanel({ summary }: Props) {
  const reconciliation = summary.mealsReconciliation;

  return (
    <Card size="small" title={`${MONTHLY_COST_TEXT.summary.periodMonth} ${summary.periodMonth}`}>
      <Descriptions column={{ xs: 1, sm: 2, md: 3 }} bordered size="small" style={{ marginBottom: 16 }}>
        <Descriptions.Item label={MONTHLY_COST_TEXT.summary.totalAmount}>
          {formatCurrency(summary.totalAmount)}
        </Descriptions.Item>
        <Descriptions.Item label={MONTHLY_COST_TEXT.summary.itemCount}>{summary.items.length}</Descriptions.Item>
        {summary.isLocked ? (
          <Descriptions.Item label={MONTHLY_COST_TEXT.summary.locked}>
            <Tag color="red">{MONTHLY_COST_TEXT.summary.locked}</Tag>
          </Descriptions.Item>
        ) : null}
      </Descriptions>

      {reconciliation.importedAmount !== null ? (
        <Alert
          type={reconciliation.hasDiscrepancy ? 'warning' : 'success'}
          showIcon
          style={{ marginBottom: 16 }}
          message={MONTHLY_COST_TEXT.reconciliation.title}
          description={`${MONTHLY_COST_TEXT.reconciliation.expected}: ${formatCurrency(reconciliation.expectedAmount)} — ${MONTHLY_COST_TEXT.reconciliation.imported}: ${formatCurrency(reconciliation.importedAmount)}. ${reconciliation.hasDiscrepancy ? MONTHLY_COST_TEXT.reconciliation.mismatch : MONTHLY_COST_TEXT.reconciliation.match}`}
        />
      ) : null}

      <Table<MonthlyCostItem>
        rowKey="monthlyCostId"
        size="small"
        columns={columns}
        dataSource={summary.items}
        pagination={{ pageSize: 10 }}
        scroll={{ x: 'max-content' }}
        locale={{ emptyText: MESSAGES.common.empty }}
      />
    </Card>
  );
}
