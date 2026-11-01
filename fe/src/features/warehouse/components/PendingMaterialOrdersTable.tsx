import { FolderOpenOutlined } from '@ant-design/icons';
import { Alert, Tag, type TableColumnsType } from 'antd';
import { DataTable } from '@/components/common/DataTable';
import { DebouncedSearch } from '@/components/common/DebouncedSearch';
import { FilterSelect } from '@/components/common/FilterSelect';
import { IconAction } from '@/components/common/IconAction';
import { QueryError } from '@/components/common/QueryError';
import { TableToolbar } from '@/components/common/TableToolbar';
import { MESSAGES } from '@/constants/messages';
import { useTableQuery } from '@/hooks/useTableQuery';
import { formatDate, formatQty } from '@/utils/format';
import { INCOMING_TEXT } from '../constants';
import { usePendingMaterialOrders } from '../hooks/useOrderMaterials';
import {
  PENDING_MATERIAL_STATE,
  type PendingMaterialOrder,
  type PendingMaterialState,
} from '../types';

const text = INCOMING_TEXT;

type Filters = { keyword?: string; materialState?: PendingMaterialState };

const STATE_OPTIONS = [
  { value: PENDING_MATERIAL_STATE.NotDeclared, label: text.notDeclared },
  { value: PENDING_MATERIAL_STATE.Awaiting, label: text.awaiting },
  { value: PENDING_MATERIAL_STATE.Partial, label: text.partial },
  { value: PENDING_MATERIAL_STATE.Complete, label: text.materialComplete },
];

const statusTag = (o: PendingMaterialOrder) => {
  if (o.materialStatus === PENDING_MATERIAL_STATE.Complete)
    return <Tag color="green">{text.materialComplete}</Tag>;
  if (o.materialLines === 0) return <Tag color="red">{text.notDeclared}</Tag>;
  return o.materialStatus === PENDING_MATERIAL_STATE.Partial ? (
    <Tag color="gold">{text.partial}</Tag>
  ) : (
    <Tag color="orange">{text.awaiting}</Tag>
  );
};

/**
 * Đơn đang chạy mà NVL chưa về đủ, phân trang và lọc ở server; đơn mới chưa khai báo NVL lên đầu để Quản đốc / Admin thấy ngay
 * việc cần làm. Nút Mở đơn ở cột thao tác mở NVL của đơn đó.
 */
export function PendingMaterialOrdersTable({ onPick }: { onPick: (orderId: number) => void }) {
  const table = useTableQuery<Filters>({});
  const pending = usePendingMaterialOrders(table.params);
  const unfiltered = !table.filters.keyword && !table.filters.materialState;

  const columns: TableColumnsType<PendingMaterialOrder> = [
    { title: text.order, dataIndex: 'orderCode' },
    { title: text.customerName, dataIndex: 'customerName' },
    { title: text.dueDate, dataIndex: 'dueDate', render: formatDate },
    { title: text.pendingState, key: 'status', render: (_, o) => statusTag(o) },
    { title: text.materialLines, dataIndex: 'materialLines', align: 'right' },
    { title: text.customerQty, dataIndex: 'customerQty', align: 'right', render: formatQty },
    { title: text.toBuyQty, dataIndex: 'toBuyQty', align: 'right', render: formatQty },
    {
      title: MESSAGES.common.actions,
      key: 'action',
      render: (_, o) => (
        <IconAction
          title={text.open}
          icon={<FolderOpenOutlined />}
          onClick={() => onPick(o.orderId)}
        />
      ),
    },
  ];

  return (
    <>
      <TableToolbar
        filters={
          <>
            <DebouncedSearch
              allowClear
              placeholder={text.pendingSearch}
              onSearch={(keyword) => table.setFilters({ keyword: keyword.trim() || undefined })}
              style={{ width: 260 }}
            />
            <FilterSelect<PendingMaterialState>
              value={table.filters.materialState}
              placeholder={text.pendingState}
              style={{ width: 200 }}
              options={STATE_OPTIONS}
              onChange={(materialState) => table.setFilters({ materialState })}
            />
          </>
        }
      />
      <QueryError error={pending.error} onRetry={() => void pending.refetch()} />
      {unfiltered && pending.data?.totalItems === 0 && (
        <Alert type="success" showIcon message={text.pendingEmpty} style={{ marginBottom: 12 }} />
      )}
      <DataTable
        rowKey="orderId"
        columns={columns}
        dataSource={pending.data?.items}
        loading={pending.isFetching}
        pagination={table.pagination(pending.data?.totalItems)}
      />
    </>
  );
}
