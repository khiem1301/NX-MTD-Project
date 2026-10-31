import { useState } from 'react';
import { Button, Tag } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { QueryError } from '@/components/common/QueryError';
import { ReasonModal } from '@/components/common/ReasonModal';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { formatCurrency, formatDate } from '@/utils/format';
import { PRODUCTION_TEXT } from '../constants';
import { useMyDefects, useObjectDefect } from '../hooks/useProduction';
import type { Defect } from '../types';
import { PageCard } from '@/components/common/PageCard';
import { DataTable } from '@/components/common/DataTable';

const text = PRODUCTION_TEXT.defects;
const mine = PRODUCTION_TEXT.myDefects;

/** UC56 — công nhân xem phiếu hàng hỏng được ghi cho mình và phản hồi khi ghi sai. */
export function MyDefectsPage() {
  const defects = useMyDefects({ page: 1, pageSize: 100 });
  const objectDefect = useObjectDefect();
  const [objecting, setObjecting] = useState<Defect | null>(null);

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
    {
      title: MESSAGES.common.actions,
      key: 'action',
      render: (_, d) =>
        d.workerObjection ? (
          <Tag color="volcano">{text.objected}</Tag>
        ) : d.status === 'Recorded' ? (
          <Button type="link" onClick={() => setObjecting(d)}>
            {mine.object}
          </Button>
        ) : null,
    },
  ];

  return (
    <PageCard title={mine.title} description={mine.subtitle}>
      <QueryError error={defects.error} onRetry={() => void defects.refetch()} />

      <DataTable<Defect>
        rowKey="defectRecordId"
        size="small"
        columns={columns}
        dataSource={defects.data}
        loading={defects.isFetching}
        locale={{ emptyText: mine.empty }}
        scroll={{ x: 'max-content' }}
        pagination={false}
      />

      <ReasonModal
        open={objecting !== null}
        title={mine.objectTitle}
        loading={objectDefect.isPending}
        onCancel={() => setObjecting(null)}
        onSubmit={(objection) =>
          objecting &&
          objectDefect.mutate(
            { id: objecting.defectRecordId, body: { objection, rowVer: objecting.rowVer } },
            { onSuccess: () => setObjecting(null) },
          )
        }
      />
    </PageCard>
  );
}
