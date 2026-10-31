import { useState } from 'react';
import { PlusOutlined, EditOutlined } from '@ant-design/icons';
import { TableToolbar } from '@/components/common/TableToolbar';
import { Button, Card, Checkbox, Popconfirm, Space, Tabs, Tag, type TableColumnsType } from 'antd';
import { AttachmentGallery } from '@/components/common/attachments';
import { QueryError } from '@/components/common/QueryError';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { useTableQuery } from '@/hooks/useTableQuery';
import { ATTACHMENT_OWNER, ATTACHMENT_PURPOSE } from '@/types/attachment';
import { formatCurrency, formatDate, formatQty } from '@/utils/format';
import { TripCompleteModal, TripStartModal } from '../components/TripModals';
import { VehicleFormModal } from '../components/VehicleFormModal';
import {
  FUEL_PHOTO_MAX,
  TRIP_STATUS,
  TRIP_STATUS_LABEL,
  VEHICLE_STATUS_LABEL,
  VL_LABELS,
  VL_MESSAGES,
} from '../constants';
import { useChangeVehicleStatus, useTrips, useVehicles } from '../hooks/useVehicleLog';
import type { Vehicle, VehicleTrip } from '../types';
import { DebouncedSearch } from '@/components/common/DebouncedSearch';
import { FilterSelect } from '@/components/common/FilterSelect';
import { DetailModal } from '@/components/common/DetailModal';
import { DataTable } from '@/components/common/DataTable';

type TripFilters = { keyword?: string; status?: string; warningsOnly?: boolean };
type VehicleFilters = { keyword?: string; status?: string };

const TRIP_STATUS_OPTIONS = Object.entries(TRIP_STATUS_LABEL).map(([value, l]) => ({
  value,
  label: l.text,
}));
const VEHICLE_STATUS_OPTIONS = Object.entries(VEHICLE_STATUS_LABEL).map(([value, l]) => ({
  value,
  label: l.text,
}));

function TripsTab() {
  const table = useTableQuery<TripFilters>({});
  const trips = useTrips(table.params);
  const [startOpen, setStartOpen] = useState(false);
  const [completing, setCompleting] = useState<VehicleTrip | null>(null);
  const [photoTrip, setPhotoTrip] = useState<VehicleTrip | null>(null);

  const columns: TableColumnsType<VehicleTrip> = [
    { title: VL_LABELS.date, dataIndex: 'tripDate', render: formatDate },
    { title: VL_LABELS.plate, dataIndex: 'plateNumber' },
    { title: VL_LABELS.driver, dataIndex: 'driverName' },
    { title: VL_LABELS.purpose, dataIndex: 'purpose', render: (v: string | null) => v ?? '—' },
    { title: VL_LABELS.odometerStart, dataIndex: 'odometerStart', align: 'right' },
    {
      title: VL_LABELS.odometerEnd,
      dataIndex: 'odometerEnd',
      align: 'right',
      render: (v: number | null) => v ?? '—',
    },
    {
      title: VL_LABELS.distance,
      dataIndex: 'distance',
      align: 'right',
      render: (v: number | null) => v ?? '—',
    },
    {
      title: VL_LABELS.fuelLiters,
      dataIndex: 'fuelLiters',
      align: 'right',
      render: (v: number | null) => (v === null ? '—' : formatQty(v)),
    },
    {
      title: VL_LABELS.fuelNormLiters,
      dataIndex: 'fuelNormLiters',
      align: 'right',
      render: (v: number | null) => (v === null ? '—' : formatQty(v)),
    },
    {
      title: VL_LABELS.fuelCost,
      dataIndex: 'fuelCost',
      align: 'right',
      render: (v: number | null) => (v === null ? '—' : formatCurrency(v)),
    },
    {
      title: VL_LABELS.warnings,
      key: 'warnings',
      render: (_, t) => (
        <Space size={4} wrap>
          {t.kmGapWarning && <Tag color="orange">{VL_LABELS.kmGap}</Tag>}
          {t.fuelOverNormWarning && <Tag color="red">{VL_LABELS.fuelOver}</Tag>}
        </Space>
      ),
    },
    {
      title: VL_LABELS.status,
      dataIndex: 'status',
      render: (v: string) => <StatusTag status={v} labels={TRIP_STATUS_LABEL} />,
    },
    {
      title: MESSAGES.common.actions,
      key: 'action',
      render: (_, t) => (
        <Space size={0}>
          {t.status === TRIP_STATUS.STARTED && (
            <Button type="link" onClick={() => setCompleting(t)}>
              {VL_LABELS.completeTrip}
            </Button>
          )}
          <Button type="link" onClick={() => setPhotoTrip(t)}>
            {VL_LABELS.photos}
          </Button>
        </Space>
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
              placeholder={MESSAGES.common.search}
              onSearch={(keyword) => table.setFilters({ keyword: keyword || undefined })}
              style={{ width: 260 }}
            />
            <FilterSelect
              value={table.filters.status}
              placeholder={VL_LABELS.status}
              options={TRIP_STATUS_OPTIONS}
              onChange={(status?: string) => table.setFilters({ status })}
              style={{ width: 160 }}
            />
            <Checkbox
              onChange={(e) => table.setFilters({ warningsOnly: e.target.checked || undefined })}
            >
              {VL_LABELS.warningsOnly}
            </Checkbox>
          </>
        }
        actions={
          <Button type="primary" icon={<PlusOutlined />} onClick={() => setStartOpen(true)}>
            {VL_LABELS.startTrip}
          </Button>
        }
      />
      <QueryError error={trips.error} onRetry={() => void trips.refetch()} />
      <DataTable
        rowKey="vehicleTripId"
        columns={columns}
        dataSource={trips.data?.items}
        loading={trips.isFetching}
        pagination={table.pagination(trips.data?.totalItems)}
      />
      <TripStartModal open={startOpen} onClose={() => setStartOpen(false)} />
      <TripCompleteModal trip={completing} onClose={() => setCompleting(null)} />
      <DetailModal
        open={photoTrip !== null}
        onClose={() => setPhotoTrip(null)}
        title={`${VL_LABELS.receiptPhotos}${photoTrip ? ` — ${photoTrip.plateNumber}` : ''}`}
        size="sm"
      >
        {photoTrip && (
          <AttachmentGallery
            ownerType={ATTACHMENT_OWNER.VehicleTrip}
            ownerId={photoTrip.vehicleTripId}
            canEdit
            maxFiles={FUEL_PHOTO_MAX}
            purpose={ATTACHMENT_PURPOSE.FuelReceipt}
          />
        )}
      </DetailModal>
    </>
  );
}

function VehiclesTab() {
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
          <Button
            type="link"
            icon={<EditOutlined />}
            onClick={() => {
              setEditing(v);
              setFormOpen(true);
            }}
          >
            {MESSAGES.common.edit}
          </Button>
          {v.status === 'ACTIVE' ? (
            <Popconfirm
              title={VL_MESSAGES.confirmDiscontinue}
              onConfirm={() => change.mutate({ id: v.vehicleId, active: false })}
            >
              <Button type="link" danger>
                {VL_LABELS.discontinue}
              </Button>
            </Popconfirm>
          ) : (
            <Button type="link" onClick={() => change.mutate({ id: v.vehicleId, active: true })}>
              {VL_LABELS.reactivate}
            </Button>
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

/** UC35: Kế toán ghi sổ chuyến xe và nhiên liệu; cảnh báo lệch km / xăng vượt định mức. */
export function VehicleLogPage() {
  return (
    <Card title={VL_LABELS.title}>
      <Tabs
        items={[
          { key: 'trips', label: VL_LABELS.tripsTab, children: <TripsTab /> },
          { key: 'vehicles', label: VL_LABELS.vehiclesTab, children: <VehiclesTab /> },
        ]}
      />
    </Card>
  );
}
