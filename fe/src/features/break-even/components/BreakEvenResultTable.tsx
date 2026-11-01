import { Alert, Button, Space, Switch, Tag, Tooltip, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { DownloadOutlined } from '@ant-design/icons';
import { formatDateTime } from '@/utils/format';
import { BREAK_EVEN_TEXT, RISK_LABEL } from '../constants';
import { useExportBreakEven } from '../hooks/useBreakEven';
import type { BreakEvenFilter, BreakEvenResultItem } from '../types';
import { formatCurrency } from '../utils';
import { DataTable } from '@/components/common/DataTable';

type Props = {
  items: BreakEvenResultItem[];
  filter: BreakEvenFilter;
  onFilterChange: (filter: BreakEvenFilter) => void;
};

const columns: ColumnsType<BreakEvenResultItem> = [
  {
    title: BREAK_EVEN_TEXT.table.product,
    key: 'product',
    render: (_, row) => `${row.productCode} — ${row.productName}`,
  },
  { title: BREAK_EVEN_TEXT.table.allocationBasis, dataIndex: 'allocationBasis' },
  { title: BREAK_EVEN_TEXT.table.finishedQty, dataIndex: 'finishedQty', align: 'right' },
  {
    title: BREAK_EVEN_TEXT.table.cmtUnitPrice,
    dataIndex: 'cmtUnitPrice',
    align: 'right',
    render: (v: number) => formatCurrency(v),
  },
  {
    title: BREAK_EVEN_TEXT.table.revenue,
    dataIndex: 'revenue',
    align: 'right',
    render: (v: number) => formatCurrency(v),
  },
  {
    title: BREAK_EVEN_TEXT.table.variableCost,
    dataIndex: 'variableCost',
    align: 'right',
    render: (v: number) => formatCurrency(v),
  },
  {
    title: BREAK_EVEN_TEXT.table.allocatedFixedCost,
    dataIndex: 'allocatedFixedCost',
    align: 'right',
    render: (v: number) => formatCurrency(v),
  },
  {
    title: BREAK_EVEN_TEXT.table.breakEvenQty,
    dataIndex: 'breakEvenQty',
    align: 'right',
    render: (v: number | null) => (v === null ? '—' : v),
  },
  {
    title: BREAK_EVEN_TEXT.table.percentReached,
    dataIndex: 'percentReached',
    align: 'right',
    render: (v: number | null) => (v === null ? '—' : `${v}%`),
  },
  {
    title: BREAK_EVEN_TEXT.table.surplusValue,
    dataIndex: 'surplusValue',
    align: 'right',
    render: (v: number) => (
      <Typography.Text type={v < 0 ? 'danger' : 'success'}>{formatCurrency(v)}</Typography.Text>
    ),
  },
  {
    title: BREAK_EVEN_TEXT.table.risk,
    key: 'risk',
    render: (_, row) => {
      const label = row.isAtRisk ? RISK_LABEL.atRisk : RISK_LABEL.safe;
      const tag = <Tag color={label.color}>{label.text}</Tag>;
      return row.atRiskWarning ? <Tooltip title={row.atRiskWarning}>{tag}</Tooltip> : tag;
    },
  },
  {
    title: BREAK_EVEN_TEXT.table.calculatedAt,
    dataIndex: 'calculatedAt',
    render: (v: string) => formatDateTime(v),
  },
];

/** UC11 — kết quả hòa vốn: lần gần nhất hoặc toàn bộ lịch sử, xuất Excel theo đúng bộ lọc đang xem. */
export function BreakEvenResultTable({ items, filter, onFilterChange }: Props) {
  const exportBreakEven = useExportBreakEven();
  const atRiskWarnings = items.filter((i) => i.atRiskWarning).map((i) => i.atRiskWarning!);

  return (
    <Space direction="vertical" size={12} style={{ width: '100%' }}>
      <Space wrap style={{ justifyContent: 'space-between', width: '100%' }}>
        <Space>
          <span>{BREAK_EVEN_TEXT.viewHistory}</span>
          <Switch
            checked={filter.history ?? false}
            onChange={(history) => onFilterChange({ ...filter, history })}
          />
        </Space>
        <Button
          icon={<DownloadOutlined />}
          loading={exportBreakEven.isPending}
          onClick={() => exportBreakEven.mutate(filter)}
        >
          {BREAK_EVEN_TEXT.export}
        </Button>
      </Space>

      {atRiskWarnings.length > 0 ? (
        <Alert type="warning" showIcon message={atRiskWarnings.join(' ')} />
      ) : null}

      <DataTable<BreakEvenResultItem>
        rowKey="breakEvenResultId"
        size="small"
        columns={columns}
        dataSource={items}
        pagination={{ pageSize: 10 }}
        scroll={{ x: 'max-content' }}
      />
    </Space>
  );
}
