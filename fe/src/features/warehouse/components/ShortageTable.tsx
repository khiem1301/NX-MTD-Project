import { useState } from 'react';
import { EyeOutlined, FileAddOutlined } from '@ant-design/icons';
import { Typography, type TableColumnsType, type TablePaginationConfig } from 'antd';
import { DataTable } from '@/components/common/DataTable';
import { IconAction } from '@/components/common/IconAction';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { formatDateTime } from '@/utils/format';
import {
  INCOMING_TEXT,
  LABELS,
  SHORTAGE_OPEN_STATUS,
  SHORTAGE_RESOLUTION,
  SHORTAGE_RESOLUTION_LABEL,
  SHORTAGE_STATUS_LABEL,
  SOURCE_LABEL,
} from '../constants';
import type { ShortDeliveryListItem } from '../types';
import { ReplenishReceiveModal } from './ReplenishReceiveModal';
import { ShortDeliveryModal } from './ShortDeliveryModal';

const text = INCOMING_TEXT;

type ShortageTableProps = {
  rows: ShortDeliveryListItem[] | undefined;
  loading: boolean;
  pagination: TablePaginationConfig;
  emptyText: string | undefined;
};

const isAwaiting = (r: ShortDeliveryListItem) =>
  r.status === SHORTAGE_OPEN_STATUS && r.resolution === SHORTAGE_RESOLUTION.REPLENISH;

/**
 * Đơn thiếu NVL khi nhập trong màn NVL chờ nhận của Thủ kho. Đơn Quản đốc đã chọn giao bù thì mở form nhận hàng bù
 * (lập phiếu bổ sung); đơn khác chỉ xem. Quản đốc xử lý đơn thiếu ở màn Thiếu NVL khi nhập.
 */
export function ShortageTable({ rows, loading, pagination, emptyText }: ShortageTableProps) {
  const [receivingId, setReceivingId] = useState<number>();
  const [viewingId, setViewingId] = useState<number>();

  const columns: TableColumnsType<ShortDeliveryListItem> = [
    { title: LABELS.shortageCode, dataIndex: 'requestCode' },
    { title: LABELS.purchaseOrder, dataIndex: 'poCode', render: (v: string | null) => v ?? '—' },
    {
      title: text.order,
      dataIndex: 'orderCode',
      render: (v: string | null) => v ?? text.stockPurchase,
    },
    {
      title: LABELS.receiptSource,
      dataIndex: 'source',
      render: (v: string) => SOURCE_LABEL[v] ?? v,
    },
    { title: LABELS.receiptCode, dataIndex: 'receiptCode' },
    {
      title: LABELS.status,
      dataIndex: 'status',
      render: (v: string) => <StatusTag status={v} labels={SHORTAGE_STATUS_LABEL} />,
    },
    {
      title: LABELS.shortageResolution,
      dataIndex: 'resolution',
      render: (v: string | null) =>
        v ? (
          <StatusTag status={v} labels={SHORTAGE_RESOLUTION_LABEL} />
        ) : (
          <Typography.Text type="secondary">{text.notResolved}</Typography.Text>
        ),
    },
    { title: LABELS.submittedAt, dataIndex: 'createdAt', render: formatDateTime },
    {
      title: MESSAGES.common.actions,
      key: 'action',
      render: (_, r) =>
        isAwaiting(r) ? (
          <IconAction
            title={LABELS.createSupplementary}
            icon={<FileAddOutlined />}
            onClick={() => setReceivingId(r.requestId)}
          />
        ) : (
          <IconAction
            title={MESSAGES.common.view}
            icon={<EyeOutlined />}
            onClick={() => setViewingId(r.requestId)}
          />
        ),
    },
  ];

  return (
    <>
      <Typography.Paragraph type="secondary">{text.replenishHint}</Typography.Paragraph>
      <DataTable
        rowKey="requestId"
        columns={columns}
        dataSource={rows}
        loading={loading}
        pagination={pagination}
        locale={{ emptyText: emptyText ?? text.replenishEmpty }}
      />
      <ReplenishReceiveModal
        key={`receive-${receivingId ?? 'none'}`}
        requestId={receivingId}
        onClose={() => setReceivingId(undefined)}
      />
      <ShortDeliveryModal
        key={`view-${viewingId ?? 'none'}`}
        requestId={viewingId}
        onClose={() => setViewingId(undefined)}
      />
    </>
  );
}
