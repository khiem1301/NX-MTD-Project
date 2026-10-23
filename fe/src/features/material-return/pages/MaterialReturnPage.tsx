import { useState } from 'react';
import { Button, Card, Input, Select, Space, Table, type TableColumnsType } from 'antd';
import { Can } from '@/components/guards/Can';
import { QueryError } from '@/components/common/QueryError';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { ROLES } from '@/constants/roles';
import { useTableQuery } from '@/hooks/useTableQuery';
import { formatDateTime, formatQty } from '@/utils/format';
import { ReturnCreateModal } from '../components/ReturnCreateModal';
import { ReturnDrawer } from '../components/ReturnDrawer';
import { RETURN_LABELS, RETURN_STATUS_LABEL } from '../constants';
import { useMaterialReturns } from '../hooks/useMaterialReturn';
import type { MaterialReturnListItem } from '../types';

type Filters = { keyword?: string; status?: string };

const STATUS_OPTIONS = Object.entries(RETURN_STATUS_LABEL).map(([value, l]) => ({
  value,
  label: l.text,
}));

/** UC42: tổ cắt trả vải thừa theo phiếu cấp gốc; Thủ kho đếm và nhận vào kho NVL. */
export function MaterialReturnPage() {
  const table = useTableQuery<Filters>({});
  const returns = useMaterialReturns(table.params);
  const [viewingId, setViewingId] = useState<number>();
  const [createOpen, setCreateOpen] = useState(false);

  const columns: TableColumnsType<MaterialReturnListItem> = [
    {
      title: RETURN_LABELS.code,
      dataIndex: 'returnCode',
      render: (v: string, r) => <a onClick={() => setViewingId(r.materialReturnId)}>{v}</a>,
    },
    { title: RETURN_LABELS.issue, dataIndex: 'issueCode' },
    { title: RETURN_LABELS.order, dataIndex: 'orderCode' },
    { title: RETURN_LABELS.team, dataIndex: 'cuttingTeamName' },
    {
      title: RETURN_LABELS.totalQty,
      dataIndex: 'totalReturnedQty',
      align: 'right',
      render: formatQty,
    },
    { title: RETURN_LABELS.createdAt, dataIndex: 'createdAt', render: formatDateTime },
    {
      title: RETURN_LABELS.status,
      dataIndex: 'status',
      render: (v: string) => <StatusTag status={v} labels={RETURN_STATUS_LABEL} />,
    },
    {
      title: RETURN_LABELS.confirmedBy,
      dataIndex: 'confirmedByName',
      render: (v: string | null) => v ?? '—',
    },
  ];

  return (
    <Card
      title={RETURN_LABELS.title}
      extra={
        <Can roles={[ROLES.TeamLeader, ROLES.TeamDeputy]}>
          <Button type="primary" onClick={() => setCreateOpen(true)}>
            {RETURN_LABELS.create}
          </Button>
        </Can>
      }
    >
      <Space style={{ marginBottom: 16 }} wrap>
        <Input.Search
          allowClear
          placeholder={MESSAGES.common.search}
          onSearch={(keyword) => table.setFilters({ keyword: keyword || undefined })}
          style={{ width: 280 }}
        />
        <Select
          allowClear
          placeholder={RETURN_LABELS.status}
          options={STATUS_OPTIONS}
          onChange={(status?: string) => table.setFilters({ status })}
          style={{ width: 170 }}
        />
      </Space>
      <QueryError error={returns.error} onRetry={() => void returns.refetch()} />
      <Table
        rowKey="materialReturnId"
        columns={columns}
        dataSource={returns.data?.items}
        loading={returns.isFetching}
        pagination={table.pagination(returns.data?.totalItems)}
        locale={{ emptyText: MESSAGES.common.empty }}
      />
      <ReturnDrawer returnId={viewingId} onClose={() => setViewingId(undefined)} />
      <ReturnCreateModal open={createOpen} onClose={() => setCreateOpen(false)} />
    </Card>
  );
}
