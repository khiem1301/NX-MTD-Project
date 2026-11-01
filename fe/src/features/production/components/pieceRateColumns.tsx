import { CloseCircleOutlined, CheckCircleOutlined } from '@ant-design/icons';
import { Space } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { IconAction } from '@/components/common/IconAction';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { formatCurrency, formatDate } from '@/utils/format';
import type { PieceRate } from '../types';
import { PRODUCTION_TEXT } from '../constants';

const text = PRODUCTION_TEXT.pieceRates;

type PieceRateColumnsOptions = {
  onApprove: (rate: PieceRate) => void;
  onReject: (rate: PieceRate) => void;
};

/** Cột bảng đơn giá công đoạn chờ Admin duyệt: mã hàng, công đoạn, đơn giá, ngày hiệu lực, trạng thái, ghi chú và nút Duyệt / Từ chối. */
export const pieceRateColumns = ({
  onApprove,
  onReject,
}: PieceRateColumnsOptions): ColumnsType<PieceRate> => [
  { title: text.columns.product, dataIndex: 'productCode' },
  {
    title: text.columns.operation,
    key: 'op',
    render: (_, r) => `${r.operationCode} - ${r.operationName}`,
  },
  {
    title: text.columns.rate,
    dataIndex: 'rate',
    align: 'right',
    render: (v: number) => formatCurrency(v),
  },
  {
    title: text.columns.effectiveDate,
    dataIndex: 'effectiveDate',
    render: (v: string) => formatDate(v),
  },
  {
    title: text.columns.status,
    dataIndex: 'status',
    render: (v: string) => <StatusTag status={v} labels={text.status} />,
  },
  {
    title: text.columns.note,
    key: 'note',
    render: (_, r) => r.rejectReason ?? r.approvalNote ?? '—',
  },
  {
    title: MESSAGES.common.actions,
    key: 'action',
    render: (_, row) =>
      row.status === 'PendingApproval' ? (
        <Space size={0} wrap>
          <IconAction
            title={text.approve}
            icon={<CheckCircleOutlined />}
            onClick={() => onApprove(row)}
          />
          <IconAction
            title={text.reject}
            icon={<CloseCircleOutlined />}
            danger
            onClick={() => onReject(row)}
          />
        </Space>
      ) : null,
  },
];
