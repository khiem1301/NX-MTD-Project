import { Button, Space, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { REQUEST_STATUS_LABEL, type RequestStatus } from '@/constants/requestTypes';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { formatCurrency, formatDate, formatDateTime } from '@/utils/format';
import { PRODUCTION_TEXT } from '../constants';
import type { Defect } from '../types';

const text = PRODUCTION_TEXT.defects;

type DefectColumnsOptions = {
  isManager: boolean;
  onReplace: (defect: Defect) => void;
  onCancel: (defect: Defect) => void;
};

/** Cột bảng hàng hỏng: mã, ngày, lý do, đơn giá trừ, trạng thái, phản đối của công nhân và thao tác cắt bù / hủy phiếu. */
export const defectColumns = ({
  isManager,
  onReplace,
  onCancel,
}: DefectColumnsOptions): ColumnsType<Defect> => [
  { title: text.columns.code, dataIndex: 'defectCode' },
  { title: text.columns.date, dataIndex: 'workDate', render: (v: string) => formatDate(v) },
  ...(isManager ? [{ title: text.columns.team, dataIndex: 'sewingTeamName' }] : []),
  {
    responsive: ['md'],
    title: text.columns.order,
    key: 'order',
    render: (_, d) => `${d.orderCode} · ${d.productCode}`,
  },
  {
    title: text.columns.operation,
    key: 'op',
    render: (_, d) => `${d.operationCode} - ${d.operationName}`,
  },
  { title: text.columns.worker, dataIndex: 'responsibleWorkerName' },
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
    title: text.columns.objection,
    key: 'objection',
    render: (_, d) =>
      d.workerObjection ? (
        <Space direction="vertical" size={0}>
          <Tag color="volcano">{text.objected}</Tag>
          <Typography.Text>{d.workerObjection}</Typography.Text>
        </Space>
      ) : (
        '—'
      ),
  },
  {
    responsive: ['md'],
    title: text.columns.deadline,
    dataIndex: 'editDeadline',
    render: (v: string) => formatDateTime(v),
  },
  {
    title: PRODUCTION_TEXT.replacement.column,
    key: 'replacement',
    render: (_, d) => {
      if (d.replacementRequestCode)
        return (
          <Tag color="geekblue">
            {d.replacementRequestCode} ·{' '}
            {REQUEST_STATUS_LABEL[d.replacementRequestStatus as RequestStatus] ??
              d.replacementRequestStatus}
          </Tag>
        );
      return !isManager && d.defectType === 'Scrap' && d.status === 'Recorded' ? (
        <Button type="link" onClick={() => onReplace(d)}>
          {PRODUCTION_TEXT.replacement.button}
        </Button>
      ) : (
        '—'
      );
    },
  },
  {
    title: MESSAGES.common.actions,
    key: 'action',
    render: (_, d) =>
      d.status === 'Recorded' && (isManager || d.canEdit) ? (
        <Button type="link" danger onClick={() => onCancel(d)}>
          {text.cancel}
        </Button>
      ) : null,
  },
];
