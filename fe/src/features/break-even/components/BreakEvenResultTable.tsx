import { useState } from 'react';
import { Button, Space, Switch, Tag, Tooltip, Typography, type TableColumnsType } from 'antd';
import { DownloadOutlined } from '@ant-design/icons';
import { DataTable } from '@/components/common/DataTable';
import { formatCurrency, formatDateTime, formatQty } from '@/utils/format';
import { BREAK_EVEN_TEXT, RISK_LABEL } from '../constants';
import { useExportBreakEven } from '../hooks/useBreakEven';
import type { BreakEvenFilter, BreakEvenResultItem, BreakEvenRow } from '../types';
import { BreakEvenDetailModal } from './BreakEvenDetailModal';

const T = BREAK_EVEN_TEXT.table;
const EMPTY = '—';

const surplus = (v: number | undefined) =>
  v === undefined ? (
    EMPTY
  ) : (
    <Typography.Text type={v < 0 ? 'danger' : 'success'}>{formatCurrency(v)}</Typography.Text>
  );

/** Bốn cột của một cách chia định phí, gom dưới một tiêu đề nhóm. */
const basisColumns = (
  title: string,
  pick: (r: BreakEvenRow) => BreakEvenResultItem | null,
): TableColumnsType<BreakEvenRow>[number] => ({
  title,
  children: [
    {
      title: T.allocatedFixedCost,
      key: `${title}-fixed`,
      align: 'right',
      render: (_, r) => (pick(r) ? formatCurrency(pick(r)!.allocatedFixedCost) : EMPTY),
    },
    {
      title: T.breakEvenQty,
      key: `${title}-qty`,
      align: 'right',
      render: (_, r) => {
        const v = pick(r)?.breakEvenQty;
        return v === null || v === undefined ? EMPTY : formatQty(v);
      },
    },
    {
      title: T.percentReached,
      key: `${title}-pct`,
      align: 'right',
      render: (_, r) => {
        const v = pick(r)?.percentReached;
        return v === null || v === undefined ? EMPTY : `${v}%`;
      },
    },
    {
      title: T.surplusValue,
      key: `${title}-surplus`,
      align: 'right',
      render: (_, r) => surplus(pick(r)?.surplusValue),
    },
  ],
});

/** Nhãn rủi ro: lỗ theo cách nào (hoặc cả hai). */
const riskTag = (r: BreakEvenRow) => {
  const output = r.output?.isAtRisk ?? false;
  const labour = r.labour?.isAtRisk ?? false;
  if (!output && !labour) return <Tag color={RISK_LABEL.safe.color}>{RISK_LABEL.safe.text}</Tag>;
  const which =
    output && labour
      ? ''
      : ` (${output ? BREAK_EVEN_TEXT.basis.Output : BREAK_EVEN_TEXT.basis.LabourHours})`.toLowerCase();
  const warning = (r.output?.atRiskWarning ?? r.labour?.atRiskWarning) || undefined;
  return (
    <Tooltip title={warning}>
      <Tag color={RISK_LABEL.atRisk.color}>{`${RISK_LABEL.atRisk.text}${which}`}</Tag>
    </Tooltip>
  );
};

/** Kết quả hòa vốn: mỗi mã hàng một dòng, hai nhóm cột theo sản lượng / theo giờ công; bấm dòng mở chi tiết. */
export function BreakEvenResultTable({
  rows,
  filter,
  onFilterChange,
}: {
  rows: BreakEvenRow[];
  filter: BreakEvenFilter;
  onFilterChange: (filter: BreakEvenFilter) => void;
}) {
  const exportExcel = useExportBreakEven();
  const [viewing, setViewing] = useState<BreakEvenRow | null>(null);
  const columns: TableColumnsType<BreakEvenRow> = [
    ...(filter.history
      ? [
          {
            title: T.calculatedAt,
            dataIndex: 'calculatedAt',
            render: (v: string) => formatDateTime(v),
          },
        ]
      : []),
    { title: T.product, dataIndex: 'productCode', fixed: 'left' },
    { title: T.finishedQty, dataIndex: 'finishedQty', align: 'right', render: formatQty },
    { title: T.cmtUnitPrice, dataIndex: 'cmtUnitPrice', align: 'right', render: formatCurrency },
    { title: T.revenue, dataIndex: 'revenue', align: 'right', render: formatCurrency },
    { title: T.variableCost, dataIndex: 'variableCost', align: 'right', render: formatCurrency },
    basisColumns(BREAK_EVEN_TEXT.basis.Output, (r) => r.output),
    basisColumns(BREAK_EVEN_TEXT.basis.LabourHours, (r) => r.labour),
    { title: T.risk, key: 'risk', render: (_, r) => riskTag(r) },
  ];

  return (
    <Space direction="vertical" size={12} style={{ width: '100%' }}>
      <Space style={{ width: '100%', justifyContent: 'space-between' }} wrap>
        <Space>
          <Switch
            checked={filter.history ?? false}
            onChange={(history) => onFilterChange({ ...filter, history })}
          />
          <Typography.Text>{BREAK_EVEN_TEXT.viewHistory}</Typography.Text>
        </Space>
        <Button
          icon={<DownloadOutlined />}
          loading={exportExcel.isPending}
          onClick={() => exportExcel.mutate(filter)}
        >
          {BREAK_EVEN_TEXT.export}
        </Button>
      </Space>
      <DataTable<BreakEvenRow>
        rowKey="key"
        size="small"
        bordered
        columns={columns}
        dataSource={rows}
        pagination={false}
        scroll={{ x: 'max-content' }}
        onRow={(r) => ({ onClick: () => setViewing(r), style: { cursor: 'pointer' } })}
      />
      <BreakEvenDetailModal row={viewing} onClose={() => setViewing(null)} />
    </Space>
  );
}
