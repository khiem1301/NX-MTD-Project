import { useState } from 'react';
import { Alert, Card, Col, Empty, Row, Space, Spin, Statistic, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import dayjs, { type Dayjs } from 'dayjs';
import { QueryError } from '@/components/common/QueryError';
import { formatDate, formatQty, toApiDate } from '@/utils/format';
import { PAYROLL_TEXT } from '../constants';
import { useMyEstimatedPay, useMyEstimatedPayDay } from '../hooks/usePayroll';
import type { MyEstimatedPayDay, MyOutputLine, MyScrapLine } from '../types';
import { formatCurrency } from '../utils';
import { PageCard } from '@/components/common/PageCard';
import { DetailModal } from '@/components/common/DetailModal';
import { DetailLink } from '@/components/common/DetailLink';
import { DataTable } from '@/components/common/DataTable';
import { AppMonthPicker } from '@/components/common/DateInputs';

const text = PAYROLL_TEXT.estimatedPay;
const detailText = text.detail;

const outputColumns: ColumnsType<MyOutputLine> = [
  {
    title: detailText.operation,
    key: 'operation',
    render: (_, l) => `${l.operationCode} - ${l.operationName}`,
  },
  { title: detailText.shift, dataIndex: 'shiftCode' },
  { title: detailText.order, dataIndex: 'orderCode' },
  { title: detailText.product, dataIndex: 'productCode' },
  {
    title: detailText.qty,
    dataIndex: 'quantity',
    align: 'right',
    render: (v: number) => formatQty(v),
  },
  {
    title: detailText.rate,
    dataIndex: 'unitRate',
    align: 'right',
    render: (v: number) => formatCurrency(v),
  },
  {
    title: detailText.amount,
    dataIndex: 'amount',
    align: 'right',
    render: (v: number) => formatCurrency(v),
  },
];

const scrapColumns: ColumnsType<MyScrapLine> = [
  {
    title: detailText.operation,
    key: 'operation',
    render: (_, l) => `${l.operationCode} - ${l.operationName}`,
  },
  { title: detailText.order, dataIndex: 'orderCode' },
  { title: detailText.product, dataIndex: 'productCode' },
  {
    title: detailText.qty,
    dataIndex: 'quantity',
    align: 'right',
    render: (v: number) => formatQty(v),
  },
  {
    title: detailText.rate,
    dataIndex: 'scrapUnitRate',
    align: 'right',
    render: (v: number) => formatCurrency(v),
  },
  {
    title: detailText.amount,
    dataIndex: 'amount',
    align: 'right',
    render: (v: number) => formatCurrency(v),
  },
];

/** Dòng mở rộng của một ngày: chỉ tải khi mở (mỗi ngày một truy vấn). */
function DayDetail({ date }: { date: string }) {
  const detail = useMyEstimatedPayDay(date);
  if (detail.isLoading) return <Spin />;
  if (detail.error)
    return <QueryError error={detail.error} onRetry={() => void detail.refetch()} />;

  const { outputs = [], scraps = [] } = detail.data ?? {};
  return (
    <Space direction="vertical" style={{ width: '100%' }} size={12}>
      <div>
        <Typography.Text strong>{detailText.outputs}</Typography.Text>
        <DataTable<MyOutputLine>
          rowKey={(l) => `${l.shiftCode}-${l.operationCode}-${l.orderCode}-${l.productCode}`}
          size="small"
          columns={outputColumns}
          dataSource={outputs}
          pagination={false}
          locale={{ emptyText: detailText.none }}
          scroll={{ x: 'max-content' }}
        />
      </div>
      {scraps.length > 0 && (
        <div>
          <Typography.Text strong>{detailText.scraps}</Typography.Text>
          <DataTable<MyScrapLine>
            rowKey={(l) => `${l.operationCode}-${l.orderCode}-${l.productCode}-${l.quantity}`}
            size="small"
            columns={scrapColumns}
            dataSource={scraps}
            pagination={false}
            scroll={{ x: 'max-content' }}
          />
        </div>
      )}
    </Space>
  );
}

/**
 * UC59 (MI-10) — công nhân may khoán sản phẩm xem sản lượng và lương tạm tính của chính mình theo tháng.
 * Luôn ghi rõ "tạm tính" (GBR-12): lương chính thức chỉ xem ở Phiếu lương sau khi Admin duyệt.
 */
export function MyEstimatedPayPage() {
  const [month, setMonth] = useState<Dayjs>(dayjs());
  const from = toApiDate(month.startOf('month'));
  const to = toApiDate(month.endOf('month'));
  const pay = useMyEstimatedPay(from, to);

  const [viewingDate, setViewingDate] = useState<string | null>(null);

  const columns: ColumnsType<MyEstimatedPayDay> = [
    {
      title: text.columns.date,
      dataIndex: 'workDate',
      render: (value: string, row) => (
        <Space>
          <DetailLink onClick={() => setViewingDate(row.workDate)}>{formatDate(value)}</DetailLink>
          {row.isSunday && <Tag color="gold">{text.columns.sunday}</Tag>}
        </Space>
      ),
    },
    {
      title: text.columns.output,
      dataIndex: 'outputQty',
      align: 'right',
      render: (v: number) => formatQty(v),
    },
    {
      title: text.columns.scrap,
      dataIndex: 'scrapQty',
      align: 'right',
      render: (v: number) => formatQty(v),
    },
    {
      title: text.columns.outputAmount,
      dataIndex: 'outputAmount',
      align: 'right',
      render: (v: number) => formatCurrency(v),
    },
    {
      title: text.columns.sundayBonus,
      dataIndex: 'sundayBonus',
      align: 'right',
      render: (v: number) => formatCurrency(v),
    },
    {
      title: text.columns.scrapAmount,
      dataIndex: 'scrapAmount',
      align: 'right',
      render: (v: number) => formatCurrency(v),
    },
    {
      title: text.columns.estimatedPay,
      dataIndex: 'estimatedPay',
      align: 'right',
      render: (v: number) => <Typography.Text strong>{formatCurrency(v)}</Typography.Text>,
    },
  ];

  const data = pay.data;
  return (
    <PageCard
      title={
        <Space align="center">
          {text.pageTitle}
          <Tag color="orange">{text.estimateTag}</Tag>
        </Space>
      }
      description={text.pageSubtitle}
    >
      <Space>
        <Typography.Text>{text.month}</Typography.Text>
        <AppMonthPicker
          allowClear={false}
          value={month}
          onChange={(value) => value && setMonth(value)}
        />
      </Space>

      <QueryError error={pay.error} onRetry={() => void pay.refetch()} />
      {pay.isLoading && <Spin />}

      {data && data.days.length === 0 && (
        <Alert type="info" showIcon message={text.notApplicable} />
      )}
      {data && data.days.length === 0 && <Empty />}

      {data && data.days.length > 0 && (
        <>
          <Row gutter={[16, 16]}>
            <Col xs={12} md={6}>
              <Card size="small">
                <Statistic title={text.totals.output} value={formatQty(data.totalOutputQty)} />
              </Card>
            </Col>
            <Col xs={12} md={6}>
              <Card size="small">
                <Statistic
                  title={text.totals.outputAmount}
                  value={formatCurrency(data.totalOutputAmount)}
                />
              </Card>
            </Col>
            <Col xs={12} md={6}>
              <Card size="small">
                <Statistic
                  title={`${text.totals.sundayBonus} / ${text.totals.scrap}`}
                  value={`${formatCurrency(data.totalSundayBonus)} / −${formatCurrency(data.totalScrapAmount)}`}
                />
              </Card>
            </Col>
            <Col xs={12} md={6}>
              <Card size="small">
                <Statistic
                  title={`${text.totals.estimatedPay} (${text.estimateTag})`}
                  value={formatCurrency(data.totalEstimatedPay)}
                />
              </Card>
            </Col>
          </Row>

          <DataTable<MyEstimatedPayDay>
            rowKey="workDate"
            size="small"
            columns={columns}
            dataSource={data.days}
            pagination={false}
            scroll={{ x: 'max-content' }}
          />
        </>
      )}
      <DetailModal
        open={viewingDate !== null}
        onClose={() => setViewingDate(null)}
        title={viewingDate ? formatDate(viewingDate) : undefined}
      >
        {viewingDate && <DayDetail date={viewingDate} />}
      </DetailModal>
    </PageCard>
  );
}
