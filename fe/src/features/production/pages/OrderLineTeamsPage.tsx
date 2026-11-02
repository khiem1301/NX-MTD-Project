import { useState } from 'react';
import { Checkbox, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { DataTable } from '@/components/common/DataTable';
import { DebouncedSearch } from '@/components/common/DebouncedSearch';
import { PageCard } from '@/components/common/PageCard';
import { QueryError } from '@/components/common/QueryError';
import { TableToolbar } from '@/components/common/TableToolbar';
import { useTeamOptions } from '@/hooks/useLookups';
import { useTableQuery } from '@/hooks/useTableQuery';
import { formatDate } from '@/utils/format';
import { OrderLineTeamBulkBar } from '../components/OrderLineTeamBulkBar';
import { OrderLineTeamSelect } from '../components/OrderLineTeamSelect';
import { PRODUCTION_TEXT, SEWING_TEAM_TYPE } from '../constants';
import { useOrderLineTeams } from '../hooks/useProduction';
import type { OrderLineTeamRow } from '../types';

const text = PRODUCTION_TEXT.orderLineTeams;
/** Bề rộng tối thiểu của bảng: màn hẹp hơn thì cuộn ngang, ô chọn tổ không bị cắt. */
const TABLE_MIN_WIDTH = 900;

type Filters = { keyword?: string; onlyUnallotted?: boolean };

/** DEV-ALL-49: Admin, Quản đốc chia từng dòng đơn (mã hàng × màu × size) của đơn đang mở cho tổ may. Tổ cắt chỉ có một tổ nên không chia. */
export function OrderLineTeamsPage() {
  const table = useTableQuery<Filters>({});
  const list = useOrderLineTeams(table.params);
  const teams = useTeamOptions(SEWING_TEAM_TYPE);
  const teamOptions = (teams.data ?? []).map((t) => ({ value: t.teamId, label: t.teamName }));
  const [selected, setSelected] = useState<number[]>([]);

  // Đổi bộ lọc thì bỏ chọn, để không chia nhầm dòng đang bị ẩn.
  const changeFilter = (change: Filters) => {
    table.setFilters(change);
    setSelected([]);
  };

  const columns: ColumnsType<OrderLineTeamRow> = [
    { title: text.columns.order, dataIndex: 'orderCode' },
    {
      title: text.columns.dueDate,
      dataIndex: 'dueDate',
      render: (v: string) => formatDate(v),
    },
    {
      title: text.columns.product,
      key: 'product',
      render: (_, r) => (
        <>
          <Typography.Text strong>{r.productCode}</Typography.Text>
          <br />
          <Typography.Text type="secondary">{r.productName}</Typography.Text>
        </>
      ),
    },
    { title: text.columns.color, dataIndex: 'color' },
    { title: text.columns.size, dataIndex: 'sizeCode' },
    { title: text.columns.qty, dataIndex: 'orderedQty', align: 'right' },
    {
      title: text.columns.teams,
      key: 'teams',
      width: '32%',
      render: (_, r) => (
        <OrderLineTeamSelect row={r} teamOptions={teamOptions} teamsLoading={teams.isLoading} />
      ),
    },
  ];

  return (
    <PageCard title={text.title} description={text.subtitle}>
      <TableToolbar
        filters={
          <>
            <DebouncedSearch
              allowClear
              style={{ width: 260 }}
              placeholder={text.search}
              onSearch={(keyword) => changeFilter({ keyword: keyword.trim() || undefined })}
            />
            <Checkbox
              checked={table.filters.onlyUnallotted ?? false}
              onChange={(e) => changeFilter({ onlyUnallotted: e.target.checked || undefined })}
            >
              {text.onlyUnallotted}
            </Checkbox>
          </>
        }
      />
      <QueryError error={list.error} onRetry={() => void list.refetch()} />
      {/* Chung một khối để thanh chia dính được trong suốt chiều dài bảng. */}
      <div>
        <DataTable<OrderLineTeamRow>
          rowKey={(r) => r.orderLineId}
          columns={columns}
          dataSource={list.data?.items ?? []}
          loading={list.isLoading}
          locale={{ emptyText: text.empty }}
          pagination={table.pagination(list.data?.totalItems)}
          scroll={{ x: TABLE_MIN_WIDTH }}
          rowSelection={{
            selectedRowKeys: selected,
            preserveSelectedRowKeys: true,
            onChange: (keys) => setSelected(keys as number[]),
          }}
        />
        {selected.length > 0 && (
          <OrderLineTeamBulkBar
            orderLineIds={selected}
            teamOptions={teamOptions}
            onDone={() => setSelected([])}
          />
        )}
      </div>
    </PageCard>
  );
}
