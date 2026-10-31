import { useState } from 'react';
import { PlusOutlined } from '@ant-design/icons';
import { TableToolbar } from '@/components/common/TableToolbar';
import { Button, Checkbox, Space, Tag, type TableColumnsType } from 'antd';
import { AttachmentGallery } from '@/components/common/attachments';
import { QueryError } from '@/components/common/QueryError';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { useTableQuery } from '@/hooks/useTableQuery';
import { ATTACHMENT_OWNER, ATTACHMENT_PURPOSE } from '@/types/attachment';
import { formatCurrency, formatDate, formatQty } from '@/utils/format';
import { TripStartModal } from './TripModals';
import { FUEL_PHOTO_MAX, TRIP_STATUS, TRIP_STATUS_LABEL, VL_LABELS } from '../constants';
import { useTrips } from '../hooks/useVehicleLog';
import type { VehicleTrip } from '../types';
import { DebouncedSearch } from '@/components/common/DebouncedSearch';
import { FilterSelect } from '@/components/common/FilterSelect';
import { DetailModal } from '@/components/common/DetailModal';
import { DataTable } from '@/components/common/DataTable';
import { TripCompleteModal } from './TripCompleteModal';

type TripFilters = { keyword?: string; status?: string; warningsOnly?: boolean };

const TRIP_STATUS_OPTIONS = Object.entries(TRIP_STATUS_LABEL).map(([value, l]) => ({
  value,
  label: l.text,
}));

export function TripsTab() {
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
