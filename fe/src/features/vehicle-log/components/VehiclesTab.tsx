import { useState } from 'react';
import { EditOutlined, PlayCircleOutlined, StopOutlined } from '@ant-design/icons';
import { Button, Space, type TableColumnsType } from 'antd';
import { IconAction } from '@/components/common/IconAction';
import { ConfirmAction } from '@/components/common/ConfirmAction';
import { QueryError } from '@/components/common/QueryError';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { useTableQuery } from '@/hooks/useTableQuery';
import { VehicleFormModal } from './VehicleFormModal';
import { VEHICLE_STATUS_LABEL, VL_LABELS, VL_MESSAGES } from '../constants';
import { useChangeVehicleStatus, useVehicles } from '../hooks/useVehicleLog';
import type { Vehicle } from '../types';
import { DebouncedSearch } from '@/components/common/DebouncedSearch';
import { FilterSelect } from '@/components/common/FilterSelect';
import { DataTable } from '@/components/common/DataTable';

type VehicleFilters = { keyword?: string; status?: string };

const VEHICLE_STATUS_OPTIONS = Object.entries(VEHICLE_STATUS_LABEL).map(([value, l]) => ({
  value,
  label: l.text,
}));

export function VehiclesTab() {
  const table = useTableQuery<VehicleFilters>({});
  const vehicles = useVehicles(table.params);
  const change = useChangeVehicleStatus();
  const [editing, setEditing] = useState<Vehicle | null>(null);
  const [formOpen, setFormOpen] = useState(false);

  const columns: TableColumnsType<Vehicle> = [
    { title: VL_LABELS.plate, dataIndex: 'plateNumber' },
    { title: VL_LABELS.vehicleType, dataIndex: 'vehicleType' },
    { title: VL_LABELS.fuelNorm, dataIndex: 'fuelNormPer100Km', align: 'right' },
    { title: VL_LABELS.tripCount, dataIndex: 'tripCount', align: 'right' },
    {
      title: VL_LABELS.lastOdometer,
      dataIndex: 'lastOdometer',
      align: 'right',
      render: (v: number | null) => v ?? '—',
    },
    {
      title: VL_LABELS.status,
      dataIndex: 'status',
      render: (v: string) => <StatusTag status={v} labels={VEHICLE_STATUS_LABEL} />,
    },
    {
      title: MESSAGES.common.actions,
      key: 'action',
      render: (_, v) => (
        <Space size={0}>
          <IconAction
            title={MESSAGES.common.edit}
            icon={<EditOutlined />}
            onClick={() => {
              setEditing(v);
              setFormOpen(true);
            }}
          />
          {v.status === 'ACTIVE' ? (
            <ConfirmAction
              title={VL_MESSAGES.confirmDiscontinue}
              onConfirm={() => change.mutate({ id: v.vehicleId, active: false })}
            >
              <IconAction title={VL_LABELS.discontinue} icon={<StopOutlined />} danger />
            </ConfirmAction>
          ) : (
            <IconAction
              title={VL_LABELS.reactivate}
              icon={<PlayCircleOutlined />}
              onClick={() => change.mutate({ id: v.vehicleId, active: true })}
            />
          )}
        </Space>
      ),
    },
  ];

  return (
    <>
      <Space style={{ marginBottom: 16 }} wrap>
        <DebouncedSearch
          allowClear
          placeholder={MESSAGES.common.search}
          onSearch={(keyword) => table.setFilters({ keyword: keyword || undefined })}
          style={{ width: 260 }}
        />
        <FilterSelect
          value={table.filters.status}
          placeholder={VL_LABELS.status}
          options={VEHICLE_STATUS_OPTIONS}
          onChange={(status?: string) => table.setFilters({ status })}
          style={{ width: 160 }}
        />
        <Button
          type="primary"
          onClick={() => {
            setEditing(null);
            setFormOpen(true);
          }}
        >
          {VL_LABELS.addVehicle}
        </Button>
      </Space>
      <QueryError error={vehicles.error} onRetry={() => void vehicles.refetch()} />
      <DataTable
        rowKey="vehicleId"
        columns={columns}
        dataSource={vehicles.data?.items}
        loading={vehicles.isFetching}
        pagination={table.pagination(vehicles.data?.totalItems)}
      />
      <VehicleFormModal open={formOpen} vehicle={editing} onClose={() => setFormOpen(false)} />
    </>
  );
}
