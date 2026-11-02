import {
  EditOutlined,
  PlayCircleOutlined,
  StopOutlined,
  SendOutlined,
  DollarOutlined,
  EyeOutlined,
} from '@ant-design/icons';
import { Space, Tag, Typography } from 'antd';
import { IconAction } from '@/components/common/IconAction';
import { ConfirmAction } from '@/components/common/ConfirmAction';
import type { ColumnsType } from 'antd/es/table';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { fill, formatCurrency, formatDate, formatQty } from '@/utils/format';
import { PRODUCTION_TEXT } from '../constants';
import type { Operation } from '../types';
import { editableRate } from '../utils';

const text = PRODUCTION_TEXT.operations;
const rateText = PRODUCTION_TEXT.pieceRates;

/** Tiêu đề cột không xuống dòng để hàng tiêu đề gọn một dòng. */
const noWrapHeader = () => ({ style: { whiteSpace: 'nowrap' as const } });
const muted = (row: Operation) => (row.status === 'Active' ? undefined : 'secondary');

/**
 * Đơn giá đang áp dụng, bên dưới là đơn giá đã duyệt sắp áp dụng gần nhất. Bản Nháp, Chờ duyệt, Bị từ chối không hiện
 * ở đây: lọc theo ô Đơn giá hoặc xem lịch sử đơn giá của công đoạn.
 */
const renderRate = (row: Operation) => {
  const current =
    row.currentRate !== null ? (
      <Typography.Text strong>{formatCurrency(row.currentRate)}</Typography.Text>
    ) : (
      <Typography.Text type={row.status === 'Active' ? 'warning' : 'secondary'}>
        {row.status === 'Active' ? text.noRate : '—'}
      </Typography.Text>
    );
  const upcoming = row.upcomingRate;
  if (!upcoming) return current;
  return (
    <Space direction="vertical" size={0}>
      {current}
      <Typography.Text type="secondary" style={{ whiteSpace: 'nowrap' }}>
        {fill(text.nextRateLine, formatCurrency(upcoming.rate), formatDate(upcoming.effectiveDate))}
      </Typography.Text>
    </Space>
  );
};

/** Đặt / sửa đơn giá được khi công đoạn còn dùng và chưa có bản nào đang chờ Admin duyệt. */
const canSetRate = (operation: Operation) =>
  operation.status === 'Active' && operation.nextRate?.status !== 'PendingApproval';

type OperationColumnsOptions = {
  onView: (row: Operation) => void;
  onEdit: (row: Operation) => void;
  onSetRate: (row: Operation) => void;
  onSubmitRate: (pieceRateId: number) => void;
  onToggle: (row: Operation, discontinue: boolean) => void;
  /** Admin chỉ xem đơn giá, Kế toán mới đặt và gửi duyệt (DEV-ALL-50). */
  canManageRates: boolean;
};

/** Cột bảng công đoạn: thông tin công đoạn, đơn giá hiện tại / sắp áp dụng và các thao tác trên từng dòng. */
export const operationColumns = ({
  onView,
  onEdit,
  onSetRate,
  onSubmitRate,
  onToggle,
  canManageRates,
}: OperationColumnsOptions): ColumnsType<Operation> => [
  {
    title: text.columns.sequence,
    dataIndex: 'stationSequence',
    width: 72,
    align: 'center',
    onHeaderCell: noWrapHeader,
  },
  {
    title: text.columns.code,
    dataIndex: 'operationCode',
    width: 110,
    render: (v: string, row) => (
      <Space size={4} wrap>
        <Typography.Text strong type={muted(row)}>
          {v}
        </Typography.Text>
        {row.isFinalStep && <Tag color="blue">{text.columns.isFinal}</Tag>}
      </Space>
    ),
  },
  {
    title: text.columns.name,
    dataIndex: 'operationName',
    render: (v: string, row) => (
      <Space size={6} wrap>
        <Typography.Text type={muted(row)}>{v}</Typography.Text>
        {row.status !== 'Active' && <StatusTag status={row.status} labels={text.status} />}
      </Space>
    ),
  },
  {
    title: text.columns.group,
    dataIndex: 'groupName',
    width: 130,
    onHeaderCell: noWrapHeader,
    render: (v: string | null) => (v ? <Tag color="geekblue">{v}</Tag> : '—'),
  },
  {
    title: text.columns.skill,
    dataIndex: 'skillLevel',
    width: 64,
    align: 'center',
    render: (v: number | null) => v ?? '—',
  },
  {
    title: text.columns.standardTime,
    dataIndex: 'standardTimeSec',
    width: 120,
    align: 'right',
    onHeaderCell: noWrapHeader,
    render: (v: number | null) => (v === null ? '—' : formatQty(v)),
  },
  {
    title: text.columns.rate,
    key: 'rate',
    width: 230,
    render: (_, row) => renderRate(row),
  },
  {
    title: MESSAGES.common.actions,
    key: 'actions',
    width: 160,
    render: (_, row) => (
      <Space size={0}>
        <IconAction
          title={MESSAGES.common.view}
          icon={<EyeOutlined />}
          onClick={() => onView(row)}
        />
        <IconAction
          title={MESSAGES.common.edit}
          icon={<EditOutlined />}
          onClick={() => onEdit(row)}
        />
        {canManageRates && canSetRate(row) && (
          <IconAction
            title={editableRate(row) ? rateText.editRate : rateText.setRate}
            icon={<DollarOutlined />}
            onClick={() => onSetRate(row)}
          />
        )}
        {canManageRates && row.nextRate?.status === 'Draft' && (
          <ConfirmAction
            title={rateText.submitConfirm}
            onConfirm={() => onSubmitRate(row.nextRate!.pieceRateId)}
          >
            <IconAction title={rateText.submit} icon={<SendOutlined />} />
          </ConfirmAction>
        )}
        {row.status === 'Active' ? (
          <ConfirmAction
            title={text.discontinueConfirm}
            description={row.isFinalStep ? text.finalDiscontinueConfirm : undefined}
            onConfirm={() => onToggle(row, true)}
          >
            <IconAction title={text.discontinue} icon={<StopOutlined />} danger />
          </ConfirmAction>
        ) : (
          <IconAction
            title={text.reactivate}
            icon={<PlayCircleOutlined />}
            onClick={() => onToggle(row, false)}
          />
        )}
      </Space>
    ),
  },
];
