import { useState } from 'react';
import { PlusOutlined } from '@ant-design/icons';
import { Button, Card, type TableColumnsType } from 'antd';
import { Can } from '@/components/guards/Can';
import { QueryError } from '@/components/common/QueryError';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { ROLES } from '@/constants/roles';
import { useTableQuery } from '@/hooks/useTableQuery';
import { formatDateTime } from '@/utils/format';
import { FgHandoverCreateModal } from '../components/FgHandoverCreateModal';
import { FgHandoverModal } from '../components/FgHandoverModal';
import { FG_LABELS, FG_STATUS_LABEL } from '../constants';
import { useFgHandovers } from '../hooks/useFgHandover';
import type { FgHandoverListItem } from '../types';
import { DebouncedSearch } from '@/components/common/DebouncedSearch';
import { FilterSelect } from '@/components/common/FilterSelect';
import { DetailLink } from '@/components/common/DetailLink';
import { DataTable } from '@/components/common/DataTable';
import { TableToolbar } from '@/components/common/TableToolbar';

type Filters = { keyword?: string; status?: string };

const STATUS_OPTIONS = Object.entries(FG_STATUS_LABEL).map(([value, l]) => ({
  value,
  label: l.text,
}));

/** UC44: QA/QC bàn giao thành phẩm (số QC đạt) vào kho; Thủ kho đếm và nhận vào kho thành phẩm. */
export function FgHandoverPage() {
  const table = useTableQuery<Filters>({});
  const handovers = useFgHandovers(table.params);
  const [viewingId, setViewingId] = useState<number>();
  const [createOpen, setCreateOpen] = useState(false);

  const columns: TableColumnsType<FgHandoverListItem> = [
    {
      title: FG_LABELS.code,
      dataIndex: 'handoverCode',
      render: (v: string, h) => (
        <DetailLink onClick={() => setViewingId(h.fgHandoverId)}>{v}</DetailLink>
      ),
    },
    { title: FG_LABELS.order, dataIndex: 'orderCode' },
    { title: FG_LABELS.product, dataIndex: 'productCode' },
    { title: FG_LABELS.inspector, dataIndex: 'inspectorName' },
    { title: FG_LABELS.totalHandedQty, dataIndex: 'totalHandedQty', align: 'right' },
    { title: FG_LABELS.handedAt, dataIndex: 'handedAt', render: formatDateTime },
    {
      title: FG_LABELS.status,
      dataIndex: 'status',
      render: (v: string) => <StatusTag status={v} labels={FG_STATUS_LABEL} />,
    },
    {
      title: FG_LABELS.confirmedBy,
      dataIndex: 'confirmedByName',
      render: (v: string | null) => v ?? '—',
    },
  ];

  return (
    <Card
      title={FG_LABELS.title}
      extra={
        <Can roles={[ROLES.QaQc]}>
          <Button type="primary" icon={<PlusOutlined />} onClick={() => setCreateOpen(true)}>
            {FG_LABELS.create}
          </Button>
        </Can>
      }
    >
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
              value={table.filters.status}
              placeholder={FG_LABELS.status}
              options={STATUS_OPTIONS}
              onChange={(status?: string) => table.setFilters({ status })}
              style={{ width: 170 }}
            />
          </>
        }
      />
      <QueryError error={handovers.error} onRetry={() => void handovers.refetch()} />
      <DataTable
        rowKey="fgHandoverId"
        columns={columns}
        dataSource={handovers.data?.items}
        loading={handovers.isFetching}
        pagination={table.pagination(handovers.data?.totalItems)}
      />
      <FgHandoverModal handoverId={viewingId} onClose={() => setViewingId(undefined)} />
      <FgHandoverCreateModal open={createOpen} onClose={() => setCreateOpen(false)} />
    </Card>
  );
}
