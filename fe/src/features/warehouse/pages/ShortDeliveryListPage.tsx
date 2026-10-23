import { useState } from 'react';
import { Card, Input, Select, Space, Table, Typography, type TableColumnsType } from 'antd';
import { QueryError } from '@/components/common/QueryError';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { useTableQuery } from '@/hooks/useTableQuery';
import { formatDateTime } from '@/utils/format';
import { ShortDeliveryModal } from '../components/ShortDeliveryModal';
import { LABELS, SHORTAGE_RESOLUTION_LABEL, SHORTAGE_STATUS_LABEL } from '../constants';
import { useShortDeliveries } from '../hooks/useShortDeliveries';
import type { ShortDeliveryListItem } from '../types';

type Filters = { keyword?: string; awaitingReplenish?: boolean };

const SCOPE_OPTIONS = [{ value: true, label: 'Đang chờ nhận hàng bù' }];

/**
 * UC08 / UC39: đơn thiếu NVL khi nhập (hệ thống tự tạo từ phiếu nhập có hàng thiếu / lỗi). Quản đốc chọn giao bù hoặc
 * chốt không bù; Thủ kho chỉ thấy các đơn đã chọn giao bù để lập phiếu bổ sung.
 */
export function ShortDeliveryListPage() {
  const table = useTableQuery<Filters>({});
  const list = useShortDeliveries(table.params);
  const [viewingId, setViewingId] = useState<number>();

  const columns: TableColumnsType<ShortDeliveryListItem> = [
    {
      title: LABELS.shortageCode,
      dataIndex: 'requestCode',
      render: (v: string, r) => <a onClick={() => setViewingId(r.requestId)}>{v}</a>,
    },
    { title: LABELS.receiptCode, dataIndex: 'receiptCode' },
    { title: LABELS.purchaseOrder, dataIndex: 'poCode', render: (v: string | null) => v ?? '—' },
    { title: LABELS.order, dataIndex: 'orderCode', render: (v: string | null) => v ?? '—' },
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
          <Typography.Text type="secondary">Chưa xử lý</Typography.Text>
        ),
    },
    { title: LABELS.submittedAt, dataIndex: 'createdAt', render: formatDateTime },
  ];

  return (
    <Card title={LABELS.shortagesTitle}>
      <Space style={{ marginBottom: 16 }} wrap>
        <Input.Search
          allowClear
          placeholder={MESSAGES.common.search}
          onSearch={(keyword) => table.setFilters({ keyword: keyword || undefined })}
          style={{ width: 280 }}
        />
        <Select
          allowClear
          placeholder={LABELS.status}
          options={SCOPE_OPTIONS}
          onChange={(awaitingReplenish?: boolean) => table.setFilters({ awaitingReplenish })}
          style={{ width: 220 }}
        />
      </Space>
      <QueryError error={list.error} onRetry={() => void list.refetch()} />
      <Table
        rowKey="requestId"
        columns={columns}
        dataSource={list.data?.items}
        loading={list.isFetching}
        pagination={table.pagination(list.data?.totalItems)}
        locale={{ emptyText: MESSAGES.common.empty }}
      />
      <ShortDeliveryModal
        key={viewingId ?? 'none'}
        requestId={viewingId}
        onClose={() => setViewingId(undefined)}
      />
    </Card>
  );
}
