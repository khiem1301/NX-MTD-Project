import { useState } from 'react';
import type { ColumnsType } from 'antd/es/table';
import { FilterSelect } from '@/components/common/FilterSelect';
import { QueryError } from '@/components/common/QueryError';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { formatCurrency, formatDate } from '@/utils/format';
import { PRODUCTION_TEXT } from '../constants';
import { useMyDefects } from '../hooks/useDefects';
import type { Defect, DefectStatus } from '../types';
import { PageCard } from '@/components/common/PageCard';
import { DataTable } from '@/components/common/DataTable';

const text = PRODUCTION_TEXT.defects;
const mine = PRODUCTION_TEXT.myDefects;

/** Phiếu đã hủy không hiện cho công nhân nên không có trong bộ lọc. */
const STATUS_OPTIONS: { value: DefectStatus; label: string }[] = [
  { value: 'Recorded', label: text.status.Recorded.text },
  { value: 'Confirmed', label: text.status.Confirmed.text },
];

/** UC56 — công nhân chỉ xem phiếu hàng hỏng được ghi cho mình (không phản hồi / khiếu nại). */
export function MyDefectsPage() {
  const [status, setStatus] = useState<DefectStatus>();
  const defects = useMyDefects({ page: 1, pageSize: 100, status });

  const columns: ColumnsType<Defect> = [
    { title: text.columns.code, dataIndex: 'defectCode' },
    { title: text.columns.date, dataIndex: 'workDate', render: (v: string) => formatDate(v) },
    {
      responsive: ['md'],
      title: text.columns.order,
      key: 'order',
      render: (_, d) => `${d.orderCode} · ${d.productCode}`,
    },
    {
      responsive: ['md'],
      title: text.columns.operation,
      key: 'op',
      render: (_, d) => `${d.operationCode} - ${d.operationName}`,
    },
    {
      title: text.columns.type,
      dataIndex: 'defectType',
      render: (v: string) => <StatusTag status={v} labels={text.type} />,
    },
    { title: text.columns.qty, dataIndex: 'quantity', align: 'right' },
    {
      title: text.columns.reason,
      key: 'reason',
      render: (_, d) =>
        d.reasonNote
          ? `${text.reason[d.defectReasonCode]}: ${d.reasonNote}`
          : text.reason[d.defectReasonCode],
    },
    {
      responsive: ['md'],
      title: text.columns.rate,
      dataIndex: 'scrapUnitRate',
      align: 'right',
      render: (v: number | null) => (v === null ? '—' : formatCurrency(v)),
    },
    {
      title: text.columns.status,
      dataIndex: 'status',
      render: (v: string) => <StatusTag status={v} labels={text.status} />,
    },
  ];

  return (
    <PageCard title={mine.title} description={mine.subtitle}>
      <FilterSelect<DefectStatus>
        value={status}
        placeholder={text.columns.status}
        options={STATUS_OPTIONS}
        onChange={setStatus}
        style={{ width: 200 }}
      />
      <QueryError error={defects.error} onRetry={() => void defects.refetch()} />

      <DataTable<Defect>
        rowKey="defectRecordId"
        size="small"
        columns={columns}
        dataSource={defects.data}
        loading={defects.isFetching}
        locale={{ emptyText: status ? MESSAGES.common.empty : mine.empty }}
        scroll={{ x: 'max-content' }}
      />
    </PageCard>
  );
}
