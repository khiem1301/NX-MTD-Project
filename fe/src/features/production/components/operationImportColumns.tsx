import { Space, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { StatusTag } from '@/components/common/StatusTag';
import { formatCurrency, formatQty } from '@/utils/format';
import type { OperationImportRow } from '../types';
import { PRODUCTION_TEXT } from '../constants';

const text = PRODUCTION_TEXT.operations.import;

export const operationImportColumns: ColumnsType<OperationImportRow> = [
  { title: text.columns.stt, dataIndex: 'stt', width: 70 },
  { title: text.columns.code, dataIndex: 'operationCode', width: 90 },
  { title: text.columns.name, dataIndex: 'operationName', render: (v: string | null) => v ?? '—' },
  {
    title: text.columns.skill,
    dataIndex: 'skillLevel',
    width: 70,
    render: (v: number | null) => v ?? '—',
  },
  {
    title: text.columns.time,
    dataIndex: 'standardTimeSec',
    align: 'right',
    render: (v: number | null) => (v === null ? '—' : formatQty(v)),
  },
  { title: text.columns.equipment, dataIndex: 'equipment', render: (v: string | null) => v ?? '—' },
  {
    title: text.columns.rate,
    key: 'rate',
    align: 'right',
    render: (_, row) => (
      <Space direction="vertical" size={0} style={{ alignItems: 'flex-end' }}>
        <span>{row.rate === null ? '—' : formatCurrency(row.rate)}</span>
        {row.rateAction !== 'None' && (
          <StatusTag status={row.rateAction} labels={text.rateAction} />
        )}
        {row.rateNote && <Typography.Text type="secondary">{row.rateNote}</Typography.Text>}
      </Space>
    ),
  },
  {
    title: text.columns.action,
    key: 'action',
    render: (_, row) => (
      <Space direction="vertical" size={0}>
        <StatusTag status={row.action} labels={text.action} />
        {row.error && <Typography.Text type="danger">{row.error}</Typography.Text>}
      </Space>
    ),
  },
];
