import { Alert, Card, Col, Row, Statistic, Typography, type TableColumnsType } from 'antd';
import { useMemo } from 'react';
import { QueryError } from '@/components/common/QueryError';
import { REVENUE_TEXT } from '@/features/revenue/constants';
import { useRevenue } from '@/features/revenue/hooks/useRevenue';
import { formatCurrency, formatMonth, formatQty } from '@/utils/format';
import { DataTable } from '@/components/common/DataTable';

type MonthRow = { periodMonth: string; finishedQty: number; revenue: number };

const PAGE_SIZE = 100;
const MONTHS_SHOWN = 6;

/** Trang chủ Admin: doanh thu tháng này và các tháng gần đây (cùng nguồn với màn Doanh thu: SL hoàn thiện đã nhập kho TP). */
export function RevenuePanel() {
  const revenue = useRevenue('admin', { page: 1, pageSize: PAGE_SIZE });
  const items = revenue.data?.items;
  const months = useMemo<MonthRow[]>(() => {
    const byMonth = new Map<string, MonthRow>();
    (items ?? []).forEach((r) => {
      const row = byMonth.get(r.periodMonth) ?? {
        periodMonth: r.periodMonth,
        finishedQty: 0,
        revenue: 0,
      };
      row.finishedQty += r.finishedQty;
      row.revenue += r.revenue;
      byMonth.set(r.periodMonth, row);
    });
    return [...byMonth.values()]
      .sort((a, b) => b.periodMonth.localeCompare(a.periodMonth))
      .slice(0, MONTHS_SHOWN);
  }, [items]);

  const currentMonth = new Date().toISOString().slice(0, 7);
  const current = months.find((m) => m.periodMonth === currentMonth);
  const truncated = (revenue.data?.totalItems ?? 0) > PAGE_SIZE;

  const columns: TableColumnsType<MonthRow> = [
    {
      title: REVENUE_TEXT.filter.periodMonth,
      dataIndex: 'periodMonth',
      render: (v: string) => formatMonth(v),
    },
    {
      title: 'SL hoàn thiện',
      dataIndex: 'finishedQty',
      align: 'right',
      render: (v: number) => formatQty(v),
    },
    {
      title: 'Doanh thu',
      dataIndex: 'revenue',
      align: 'right',
      render: (v: number) => formatCurrency(v),
    },
  ];

  return (
    <Card title="Doanh thu" style={{ marginTop: 16 }} loading={revenue.isLoading}>
      <QueryError error={revenue.error} onRetry={() => void revenue.refetch()} />
      <Typography.Paragraph type="secondary">{REVENUE_TEXT.pageSubtitle}</Typography.Paragraph>
      <Row gutter={16} style={{ marginBottom: 16 }}>
        <Col xs={24} sm={12}>
          <Statistic
            title={`Tháng này (${currentMonth})`}
            value={formatCurrency(current?.revenue ?? 0)}
          />
        </Col>
        <Col xs={24} sm={12}>
          <Statistic title="SL hoàn thiện tháng này" value={formatQty(current?.finishedQty ?? 0)} />
        </Col>
      </Row>
      {truncated && (
        <Alert
          type="info"
          showIcon
          style={{ marginBottom: 12 }}
          message={`Chỉ tính ${PAGE_SIZE} dòng gần nhất; xem đầy đủ ở màn Doanh thu.`}
        />
      )}
      <DataTable<MonthRow>
        rowKey="periodMonth"
        size="small"
        pagination={false}
        columns={columns}
        dataSource={months}
      />
    </Card>
  );
}
