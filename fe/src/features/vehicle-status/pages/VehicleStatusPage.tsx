import { Card, Space, Tag, Tooltip, type TableColumnsType } from 'antd';
import { WarningOutlined } from '@ant-design/icons';
import { DataTable } from '@/components/common/DataTable';
import { DebouncedSearch } from '@/components/common/DebouncedSearch';
import { FilterSelect } from '@/components/common/FilterSelect';
import { QueryError } from '@/components/common/QueryError';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { useTableQuery } from '@/hooks/useTableQuery';
import {
  RUNNING_FILTER_OPTIONS,
  VEHICLE_RECORD_STATUS_LABEL,
  VS_LABELS,
  VS_MESSAGES,
} from '../constants';
import { useVehicleStatuses } from '../hooks/useVehicleStatus';
import type { VehicleLiveStatus } from '../types';

type Filters = { keyword?: string; running?: string };

/** DEV-ALL-159: Admin xem xe trong hệ thống và xe nào đang chạy (có chuyến STARTED), chỉ đọc. */
export function VehicleStatusPage() {
  const table = useTableQuery<Filters>({});
  const { running, ...rest } = table.params;
  const vehicles = useVehicleStatuses({
    ...rest,
    running: running === undefined ? undefined : running === 'true',
  });

  const columns: TableColumnsType<VehicleLiveStatus> = [
    { title: VS_LABELS.plate, dataIndex: 'plateNumber' },
    { title: VS_LABELS.vehicleType, dataIndex: 'vehicleType' },
    {
      title: VS_LABELS.running,
      dataIndex: 'isRunning',
      render: (isRunning: boolean, v) => (
        <Space size={4}>
          <Tag color={isRunning ? 'blue' : 'default'}>
            {isRunning ? VS_LABELS.running : VS_LABELS.idle}
          </Tag>
          {v.openTripCount > 1 && (
            <Tooltip title={VS_MESSAGES.multiOpenTrips}>
              <WarningOutlined style={{ color: '#faad14' }} />
              {` ${v.openTripCount} ${VS_LABELS.openTripWarning}`}
            </Tooltip>
          )}
        </Space>
      ),
    },
    { title: VS_LABELS.driver, dataIndex: 'driverName', render: (v: string | null) => v ?? '—' },
    {
      title: VS_LABELS.dispatch,
      dataIndex: 'dispatchCode',
      render: (v: string | null) => v ?? '—',
    },
    { title: VS_LABELS.tripDate, dataIndex: 'tripDate', render: (v: string | null) => v ?? '—' },
    {
      title: VS_LABELS.odometerStart,
      dataIndex: 'odometerStart',
      align: 'right',
      render: (v: number | null) => v ?? '—',
    },
    {
      title: VS_LABELS.lastOdometer,
      dataIndex: 'lastOdometer',
      align: 'right',
      render: (v: number | null) => v ?? '—',
    },
    {
      title: VS_LABELS.recordStatus,
      dataIndex: 'status',
      render: (v: string) => <StatusTag status={v} labels={VEHICLE_RECORD_STATUS_LABEL} />,
    },
  ];

  return (
    <Card title={VS_LABELS.title}>
      <Space style={{ marginBottom: 16 }} wrap>
        <DebouncedSearch
          allowClear
          placeholder={MESSAGES.common.search}
          onSearch={(keyword) => table.setFilters({ keyword: keyword || undefined })}
          style={{ width: 260 }}
        />
        <FilterSelect
          value={table.filters.running}
          placeholder={VS_LABELS.runningFilter}
          options={RUNNING_FILTER_OPTIONS}
          onChange={(value?: string) => table.setFilters({ running: value })}
          style={{ width: 180 }}
        />
      </Space>
      <QueryError error={vehicles.error} onRetry={() => void vehicles.refetch()} />
      <DataTable
        rowKey="vehicleId"
        columns={columns}
        dataSource={vehicles.data?.items}
        loading={vehicles.isFetching}
        pagination={table.pagination(vehicles.data?.totalItems)}
      />
    </Card>
  );
}
