import { useState } from 'react';
import { Button, Card, Input, Select, Space, Table, type TableColumnsType } from 'antd';
import { Can } from '@/components/guards/Can';
import { QueryError } from '@/components/common/QueryError';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { ROLES } from '@/constants/roles';
import { WAREHOUSE_TYPE_LABEL } from '@/features/warehouse/constants';
import { useTableQuery } from '@/hooks/useTableQuery';
import { formatDate, formatDateTime } from '@/utils/format';
import { StocktakeDrawer } from '../components/StocktakeDrawer';
import { StocktakeFormModal } from '../components/StocktakeFormModal';
import { ST_LABELS, STOCKTAKE_STATUS_LABEL } from '../constants';
import { useStocktakes } from '../hooks/useStocktake';
import type { Stocktake, StocktakeListItem } from '../types';

type Filters = { keyword?: string; status?: string };

const STATUS_OPTIONS = Object.entries(STOCKTAKE_STATUS_LABEL).map(([value, l]) => ({
  value,
  label: l.text,
}));

/** UC48 / UC49: Thủ kho lập phiếu kiểm kê; Quản đốc / Admin duyệt để điều chỉnh tồn kho hoặc trả lại. */
export function StocktakePage() {
  const table = useTableQuery<Filters>({});
  const stocktakes = useStocktakes(table.params);
  const [viewingId, setViewingId] = useState<number>();
  const [editing, setEditing] = useState<Stocktake | null>(null);
  const [formOpen, setFormOpen] = useState(false);

  const openForm = (stocktake: Stocktake | null) => {
    setViewingId(undefined);
    setEditing(stocktake);
    setFormOpen(true);
  };

  const columns: TableColumnsType<StocktakeListItem> = [
    {
      title: ST_LABELS.code,
      dataIndex: 'stocktakeCode',
      render: (v: string, s) => <a onClick={() => setViewingId(s.stocktakeId)}>{v}</a>,
    },
    {
      title: ST_LABELS.warehouse,
      dataIndex: 'warehouseType',
      render: (v: string) => WAREHOUSE_TYPE_LABEL[v],
    },
    { title: ST_LABELS.scope, dataIndex: 'scope', render: (v: string | null) => v ?? '—' },
    { title: ST_LABELS.date, dataIndex: 'stocktakeDate', render: formatDate },
    { title: ST_LABELS.lines, dataIndex: 'lineCount', align: 'right' },
    { title: ST_LABELS.diffLines, dataIndex: 'differenceLineCount', align: 'right' },
    { title: ST_LABELS.createdAt, dataIndex: 'createdAt', render: formatDateTime },
    {
      title: ST_LABELS.status,
      dataIndex: 'status',
      render: (v: string) => <StatusTag status={v} labels={STOCKTAKE_STATUS_LABEL} />,
    },
  ];

  return (
    <Card
      title={ST_LABELS.title}
      extra={
        <Can roles={[ROLES.WarehouseKeeper]}>
          <Button type="primary" onClick={() => openForm(null)}>
            {ST_LABELS.create}
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
          placeholder={ST_LABELS.status}
          options={STATUS_OPTIONS}
          onChange={(status?: string) => table.setFilters({ status })}
          style={{ width: 170 }}
        />
      </Space>
      <QueryError error={stocktakes.error} onRetry={() => void stocktakes.refetch()} />
      <Table
        rowKey="stocktakeId"
        columns={columns}
        dataSource={stocktakes.data?.items}
        loading={stocktakes.isFetching}
        pagination={table.pagination(stocktakes.data?.totalItems)}
        locale={{ emptyText: MESSAGES.common.empty }}
      />
      <StocktakeDrawer
        stocktakeId={viewingId}
        onClose={() => setViewingId(undefined)}
        onEdit={openForm}
      />
      <StocktakeFormModal
        key={editing?.stocktakeId ?? 'new'}
        open={formOpen}
        stocktake={editing}
        onClose={() => setFormOpen(false)}
      />
    </Card>
  );
}
