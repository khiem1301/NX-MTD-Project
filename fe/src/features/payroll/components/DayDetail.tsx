import { Space, Spin, Typography } from 'antd';
import { QueryError } from '@/components/common/QueryError';
import { useMyEstimatedPayDay } from '../hooks/usePayroll';
import type { MyOutputLine, MyScrapLine } from '../types';
import { DataTable } from '@/components/common/DataTable';
import type { ColumnsType } from 'antd/es/table';
import { formatQty } from '@/utils/format';
import { formatCurrency } from '../utils';
import { PAYROLL_TEXT } from '../constants';

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
export function DayDetail({ date }: { date: string }) {
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
