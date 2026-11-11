import { useState } from 'react';
import { PlusOutlined, EditOutlined, EyeOutlined } from '@ant-design/icons';
import { Button, Card, Space, type TableColumnsType } from 'antd';
import { FilterSelect } from '@/components/common/FilterSelect';
import { IconAction } from '@/components/common/IconAction';
import { QueryError } from '@/components/common/QueryError';
import { StatusTag } from '@/components/common/StatusTag';
import { WorkOrderSelect } from '@/components/common/WorkOrderSelect';
import { MESSAGES } from '@/constants/messages';
import { MAX_PAGE_SIZE } from '@/constants/pagination';
import { useTableQuery } from '@/hooks/useTableQuery';
import { formatDate, formatQty } from '@/utils/format';
import { LayFormModal } from '../components/LayFormModal';
import { LABELS, LAY_STATUS, LAY_STATUS_LABEL, SHIFT_LABEL, WORK_ORDER_STATUS } from '../constants';
import { useLays, useWorkOrder, useWorkOrders } from '../hooks/useCutting';
import type { CuttingLay } from '../types';
import { LayDetailModal } from '../components/LayDetailModal';
import { DataTable } from '@/components/common/DataTable';

const CUTTABLE: readonly string[] = [WORK_ORDER_STATUS.OPEN, WORK_ORDER_STATUS.CUTTING];

const STATUS_OPTIONS = Object.entries(LAY_STATUS_LABEL).map(([value, label]) => ({
  value,
  label: label.text,
}));

/** UC51: tổ trưởng / tổ phó cắt chọn lệnh cắt của tổ và nhập từng bàn cắt. */
export function CuttingLayPage() {
  const [workOrderId, setWorkOrderId] = useState<number>();
  const workOrders = useWorkOrders({ page: 1, pageSize: MAX_PAGE_SIZE });
  const workOrder = useWorkOrder(workOrderId);
  const table = useTableQuery<{ status?: string }>({});
  const lays = useLays({ ...table.params, workOrderId });
  const [editing, setEditing] = useState<CuttingLay | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const cuttable = workOrder.data !== undefined && CUTTABLE.includes(workOrder.data.status);

  const openForm = (lay: CuttingLay | null) => {
    setEditing(lay);
    setFormOpen(true);
  };

  const [viewing, setViewing] = useState<CuttingLay | null>(null);

  const columns: TableColumnsType<CuttingLay> = [
    {
      title: LABELS.workDate,
      dataIndex: 'workDate',
      render: (v: string) => formatDate(v),
    },
    { title: LABELS.shift, dataIndex: 'shiftCode', render: (v: string) => SHIFT_LABEL[v] ?? v },
    { title: LABELS.laySequence, dataIndex: 'laySequence', align: 'right' },
    { title: LABELS.fabricColor, dataIndex: 'fabricColor', render: (v: string) => v || '—' },
    {
      title: LABELS.layFabricUsed,
      dataIndex: 'fabricUsedM',
      align: 'right',
      render: (v: number | null) => (v === null ? '—' : formatQty(v)),
    },
    { title: LABELS.sizeSummary, dataIndex: 'sizeSummary' },
    {
      title: LABELS.totalCutQty,
      key: 'total',
      align: 'right',
      render: (_, l) => l.lines.reduce((s, x) => s + x.actualQty, 0),
    },
    {
      title: LABELS.status,
      dataIndex: 'status',
      render: (v: string) => <StatusTag status={v} labels={LAY_STATUS_LABEL} />,
    },
    {
      title: MESSAGES.common.actions,
      key: 'actions',
      render: (_, l) => (
        <Space size={0}>
          <IconAction
            title={MESSAGES.common.view}
            icon={<EyeOutlined />}
            onClick={() => setViewing(l)}
          />
          {l.status === LAY_STATUS.RECORDED && cuttable && (
            <IconAction
              title={MESSAGES.common.edit}
              icon={<EditOutlined />}
              onClick={() => openForm(l)}
            />
          )}
        </Space>
      ),
    },
  ];

  return (
    <Card
      title={LABELS.laysTitle}
      extra={
        <Button
          type="primary"
          icon={<PlusOutlined />}
          disabled={!cuttable}
          onClick={() => openForm(null)}
        >
          {MESSAGES.common.create}
        </Button>
      }
    >
      <Space style={{ marginBottom: 16 }} wrap>
        <WorkOrderSelect
          workOrders={workOrders.data?.items.filter((w) => CUTTABLE.includes(w.status))}
          loading={workOrders.isLoading}
          value={workOrderId}
          onChange={setWorkOrderId}
          placeholder={LABELS.workOrder}
        />
        <FilterSelect<string>
          value={table.filters.status}
          placeholder={LABELS.status}
          options={STATUS_OPTIONS}
          onChange={(status) => table.setFilters({ status })}
          style={{ width: 200 }}
        />
      </Space>
      <QueryError error={lays.error ?? workOrder.error} onRetry={() => void lays.refetch()} />
      <DataTable
        rowKey="cuttingLayId"
        columns={columns}
        locale={workOrderId === undefined ? { emptyText: LABELS.selectWorkOrderHint } : undefined}
        dataSource={workOrderId === undefined ? [] : lays.data?.items}
        loading={lays.isFetching}
        pagination={table.pagination(lays.data?.totalItems)}
      />
      <LayDetailModal lay={viewing} onClose={() => setViewing(null)} />
      {workOrder.data && (
        <LayFormModal
          open={formOpen}
          workOrder={workOrder.data}
          lay={editing}
          onClose={() => setFormOpen(false)}
        />
      )}
    </Card>
  );
}
