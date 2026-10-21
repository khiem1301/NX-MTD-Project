import { Table, Tag } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { MESSAGES } from '@/constants/messages';
import { ACCOUNTING_REPORT_TEXT } from '../constants';
import type { VehicleReport, VehicleTripReportRow } from '../types';
import { formatCurrency, formatNumber } from '../utils';

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
    <Table<VehicleTripReportRow>
      rowKey="vehicleId"
      size="small"
      columns={columns}
      dataSource={report.vehicles}
      pagination={{ pageSize: 20 }}
      scroll={{ x: 'max-content' }}
      locale={{ emptyText: MESSAGES.common.empty }}
      summary={() => (
        <Table.Summary fixed>
          <Table.Summary.Row>
            <Table.Summary.Cell index={0} colSpan={3}>
              Tổng cộng
            </Table.Summary.Cell>
            <Table.Summary.Cell index={1} align="right">
              {report.totalKm}
            </Table.Summary.Cell>
            <Table.Summary.Cell index={2} align="right">
              {formatNumber(report.totalFuelLiters)}
            </Table.Summary.Cell>
            <Table.Summary.Cell index={3} align="right">
              {formatCurrency(report.totalFuelCost)}
            </Table.Summary.Cell>
            <Table.Summary.Cell index={4} align="right">
              {report.totalKmGapWarnings}
            </Table.Summary.Cell>
            <Table.Summary.Cell index={5} align="right">
              {report.totalFuelOverNormWarnings}
            </Table.Summary.Cell>
          </Table.Summary.Row>
        </Table.Summary>
      )}
    />
  );
}
