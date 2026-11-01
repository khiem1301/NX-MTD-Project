import { Tag } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { ACCOUNTING_REPORT_TEXT } from '../constants';
import type { VehicleReport, VehicleTripReportRow } from '../types';
import { formatCurrency, formatNumber } from '../utils';
import { DataTable } from '@/components/common/DataTable';

type Props = { report: VehicleReport };

const columns: ColumnsType<VehicleTripReportRow> = [
  { title: ACCOUNTING_REPORT_TEXT.vehicleTable.plateNumber, dataIndex: 'plateNumber' },
  { title: ACCOUNTING_REPORT_TEXT.vehicleTable.vehicleType, dataIndex: 'vehicleType' },
  {
    title: ACCOUNTING_REPORT_TEXT.vehicleTable.completedTripCount,
    dataIndex: 'completedTripCount',
    align: 'right',
  },
  { title: ACCOUNTING_REPORT_TEXT.vehicleTable.totalKm, dataIndex: 'totalKm', align: 'right' },
  {
    title: ACCOUNTING_REPORT_TEXT.vehicleTable.totalFuelLiters,
    dataIndex: 'totalFuelLiters',
    align: 'right',
    render: (v: number) => formatNumber(v),
  },
  {
    title: ACCOUNTING_REPORT_TEXT.vehicleTable.totalFuelCost,
    dataIndex: 'totalFuelCost',
    align: 'right',
    render: (v: number) => formatCurrency(v),
  },
  {
    title: ACCOUNTING_REPORT_TEXT.vehicleTable.kmGapWarningCount,
    dataIndex: 'kmGapWarningCount',
    align: 'right',
    render: (v: number) => (v > 0 ? <Tag color="warning">{v}</Tag> : v),
  },
  {
    title: ACCOUNTING_REPORT_TEXT.vehicleTable.fuelOverNormWarningCount,
    dataIndex: 'fuelOverNormWarningCount',
    align: 'right',
    render: (v: number) => (v > 0 ? <Tag color="warning">{v}</Tag> : v),
  },
];

/** UC36 — km + xăng theo xe trong tháng (Vehicle/VehicleTrip, MSG30: cảnh báo km không liền chuyến trước
 * hoặc vượt định mức xăng). */
export function VehicleReportTable({ report }: Props) {
  return (
    <DataTable<VehicleTripReportRow>
      rowKey="vehicleId"
      size="small"
      columns={columns}
      dataSource={report.vehicles}
      pagination={{ pageSize: 20 }}
      scroll={{ x: 'max-content' }}
      summary={() => (
        <DataTable.Summary fixed>
          <DataTable.Summary.Row>
            <DataTable.Summary.Cell index={0} colSpan={3}>
              Tổng cộng
            </DataTable.Summary.Cell>
            <DataTable.Summary.Cell index={1} align="right">
              {report.totalKm}
            </DataTable.Summary.Cell>
            <DataTable.Summary.Cell index={2} align="right">
              {formatNumber(report.totalFuelLiters)}
            </DataTable.Summary.Cell>
            <DataTable.Summary.Cell index={3} align="right">
              {formatCurrency(report.totalFuelCost)}
            </DataTable.Summary.Cell>
            <DataTable.Summary.Cell index={4} align="right">
              {report.totalKmGapWarnings}
            </DataTable.Summary.Cell>
            <DataTable.Summary.Cell index={5} align="right">
              {report.totalFuelOverNormWarnings}
            </DataTable.Summary.Cell>
          </DataTable.Summary.Row>
        </DataTable.Summary>
      )}
    />
  );
}
