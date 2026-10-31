import { EditOutlined } from '@ant-design/icons';
import { Button, Popconfirm, Space, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { DetailLink } from '@/components/common/DetailLink';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { formatCurrency, formatDate, formatQty } from '@/utils/format';
import { PRODUCTION_TEXT } from '../constants';
import type { Operation } from '../types';
import { editableRate } from '../utils';

const text = PRODUCTION_TEXT.operations;
const rateText = PRODUCTION_TEXT.pieceRates;

/** Đặt / sửa đơn giá được khi công đoạn còn dùng và chưa có bản nào đang chờ Admin duyệt. */
const canSetRate = (operation: Operation) =>
  operation.status === 'Active' && operation.nextRate?.status !== 'PendingApproval';

type OperationColumnsOptions = {
  onView: (row: Operation) => void;
  onEdit: (row: Operation) => void;
  onSetRate: (row: Operation) => void;
  onSubmitRate: (pieceRateId: number) => void;
  onToggle: (row: Operation, discontinue: boolean) => void;
};

/** Cột bảng công đoạn: thông tin công đoạn, đơn giá hiện tại / sắp áp dụng và các thao tác trên từng dòng. */
export const operationColumns = ({
  onView,
  onEdit,
  onSetRate,
  onSubmitRate,
  onToggle,
}: OperationColumnsOptions): ColumnsType<Operation> => [
  { title: text.columns.sequence, dataIndex: 'stationSequence', width: 80 },
  {
    title: text.columns.code,
    dataIndex: 'operationCode',
    render: (v: string, row) => <DetailLink onClick={() => onView(row)}>{v}</DetailLink>,
  },
  { title: text.columns.name, dataIndex: 'operationName' },
  {
    title: text.columns.group,
    dataIndex: 'groupName',
    render: (v: string | null) => (v ? <Tag color="geekblue">{v}</Tag> : '—'),
  },
  {
    title: text.columns.usesPieces,
    dataIndex: 'usesSemiFinished',
    render: (v: boolean) =>
      v ? <Tag color="blue">{text.withPieces}</Tag> : <Tag>{text.withoutPieces}</Tag>,
  },
  { title: text.columns.skill, dataIndex: 'skillLevel', render: (v: number | null) => v ?? '—' },
  {
    title: text.columns.standardTime,
    dataIndex: 'standardTimeSec',
    align: 'right',
    render: (v: number | null) => (v === null ? '—' : formatQty(v)),
  },
  {
    title: text.columns.isFinal,
    dataIndex: 'isFinalStep',
    render: (v: boolean) => (v ? <Tag color="blue">Cuối</Tag> : '—'),
  },
  {
    title: text.columns.rate,
    dataIndex: 'currentRate',
    align: 'right',
    render: (v: number | null) => (v === null ? '—' : formatCurrency(v)),
  },
  {
    title: text.columns.nextRate,
    key: 'nextRate',
    render: (_, row) =>
      row.nextRate ? (
        <Space direction="vertical" size={0}>
          <span>
            {formatCurrency(row.nextRate.rate)} · từ {formatDate(row.nextRate.effectiveDate)}
          </span>
          <StatusTag status={row.nextRate.status} labels={rateText.status} />
          {row.nextRate.rejectReason && (
            <Typography.Text type="danger">{row.nextRate.rejectReason}</Typography.Text>
          )}
        </Space>
      ) : (
        '—'
      ),
  },
  {
    title: text.columns.status,
    dataIndex: 'status',
    render: (v: string) => <StatusTag status={v} labels={text.status} />,
  },
  {
    title: MESSAGES.common.actions,
    key: 'action',
    render: (_, row) => (
      <Space size={0} wrap>
        <Button type="link" icon={<EditOutlined />} onClick={() => onEdit(row)}>
          {MESSAGES.common.edit}
        </Button>
        {canSetRate(row) && (
          <Button type="link" onClick={() => onSetRate(row)}>
            {editableRate(row) ? rateText.editRate : rateText.setRate}
          </Button>
        )}
        {row.nextRate?.status === 'Draft' && (
          <Popconfirm
            title={rateText.submitConfirm}
            onConfirm={() => onSubmitRate(row.nextRate!.pieceRateId)}
          >
            <Button type="link">{rateText.submit}</Button>
          </Popconfirm>
        )}
        {row.status === 'Active' ? (
          <Popconfirm title={text.discontinueConfirm} onConfirm={() => onToggle(row, true)}>
            <Button type="link" danger>
              {text.discontinue}
            </Button>
          </Popconfirm>
        ) : (
          <Button type="link" onClick={() => onToggle(row, false)}>
            {text.reactivate}
          </Button>
        )}
      </Space>
    ),
  },
];
