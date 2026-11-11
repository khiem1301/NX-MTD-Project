import { IconAction } from '@/components/common/IconAction';
import { EyeOutlined } from '@ant-design/icons';
import { useState } from 'react';
import { Card, Typography, type TableColumnsType } from 'antd';
import { QueryError } from '@/components/common/QueryError';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { useTableQuery } from '@/hooks/useTableQuery';
import { formatDateTime } from '@/utils/format';
import { ShortDeliveryModal } from '../components/ShortDeliveryModal';
import {
  LABELS,
  SHORTAGE_RESOLUTION_LABEL,
  SHORTAGE_SCOPE_AWAITING,
  SHORTAGE_SCOPE_OPTIONS,
  SHORTAGE_STATUS_LABEL,
  SOURCE_LABEL,
} from '../constants';
import { useShortDeliveries } from '../hooks/useShortDeliveries';
import type { ShortDeliveryListItem } from '../types';
import { DebouncedSearch } from '@/components/common/DebouncedSearch';
import { FilterSelect } from '@/components/common/FilterSelect';
import { DataTable } from '@/components/common/DataTable';
import { TableToolbar } from '@/components/common/TableToolbar';

type Filters = { keyword?: string; awaitingReplenish?: boolean };

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
    },
    { title: LABELS.receiptCode, dataIndex: 'receiptCode' },
    {
      title: LABELS.receiptSource,
      dataIndex: 'source',
      render: (v: string) => SOURCE_LABEL[v] ?? v,
    },
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
    {
      title: MESSAGES.common.actions,
      key: 'action',
      render: (_, r) => (
        <IconAction
          title={MESSAGES.common.view}
          icon={<EyeOutlined />}
          onClick={() => setViewingId(r.requestId)}
        />
      ),
    },
  ];

  return (
    <Card title={LABELS.shortagesTitle}>
      <TableToolbar
        filters={
          <>
            <DebouncedSearch
              allowClear
              placeholder={MESSAGES.common.search}
              onSearch={(keyword) => table.setFilters({ keyword: keyword || undefined })}
              style={{ width: 280 }}
            />
            <FilterSelect
              value={table.filters.awaitingReplenish ? SHORTAGE_SCOPE_AWAITING : undefined}
              placeholder={LABELS.status}
              options={SHORTAGE_SCOPE_OPTIONS}
              onChange={(scope) =>
                table.setFilters({
                  awaitingReplenish: scope === SHORTAGE_SCOPE_AWAITING ? true : undefined,
                })
              }
              style={{ width: 220 }}
            />
          </>
        }
      />
      <QueryError error={list.error} onRetry={() => void list.refetch()} />
      <DataTable
        rowKey="requestId"
        columns={columns}
        dataSource={list.data?.items}
        loading={list.isFetching}
        pagination={table.pagination(list.data?.totalItems)}
      />
      <ShortDeliveryModal
        key={viewingId ?? 'none'}
        requestId={viewingId}
        onClose={() => setViewingId(undefined)}
      />
    </Card>
  );
}
