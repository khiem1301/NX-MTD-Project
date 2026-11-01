import {} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { ACCOUNTING_REPORT_TEXT } from '../constants';
import type { WorkdayOvertimeReport, WorkdayOvertimeRow } from '../types';
import { formatNumber } from '../utils';
import { DataTable } from '@/components/common/DataTable';

type Props = { report: WorkdayOvertimeReport };

const columns: ColumnsType<WorkdayOvertimeRow> = [
  { title: ACCOUNTING_REPORT_TEXT.workdayTable.employeeCode, dataIndex: 'employeeCode' },
  { title: ACCOUNTING_REPORT_TEXT.workdayTable.fullName, dataIndex: 'fullName' },
  {
    title: ACCOUNTING_REPORT_TEXT.workdayTable.teamName,
    dataIndex: 'teamName',
    render: (v: string | null) => v ?? '—',
  },
  {
    title: ACCOUNTING_REPORT_TEXT.workdayTable.regularWorkdays,
    dataIndex: 'regularWorkdays',
    align: 'right',
    render: (v: number) => formatNumber(v),
  },
  {
    title: ACCOUNTING_REPORT_TEXT.workdayTable.sundayWorkdays,
    dataIndex: 'sundayWorkdays',
    align: 'right',
    render: (v: number) => formatNumber(v),
  },
  {
    title: ACCOUNTING_REPORT_TEXT.workdayTable.totalWorkdays,
    dataIndex: 'totalWorkdays',
    align: 'right',
    render: (v: number) => formatNumber(v),
  },
  {
    title: ACCOUNTING_REPORT_TEXT.workdayTable.totalOvertimeHours,
    dataIndex: 'totalOvertimeHours',
    align: 'right',
    render: (v: number) => formatNumber(v),
  },
];

/** UC36 — công + tăng ca theo nhân viên trong tháng (DailyTimesheet), tách công Chủ nhật. */
export function WorkdayOvertimeTable({ report }: Props) {
  return (
    <DataTable<WorkdayOvertimeRow>
      rowKey="employeeId"
      size="small"
      columns={columns}
      dataSource={report.rows}
      pagination={{ pageSize: 20 }}
      scroll={{ x: 'max-content' }}
      summary={() => (
        <DataTable.Summary fixed>
          <DataTable.Summary.Row>
            <DataTable.Summary.Cell index={0} colSpan={3}>
              Tổng cộng
            </DataTable.Summary.Cell>
            <DataTable.Summary.Cell index={1} align="right">
              {formatNumber(report.totalRegularWorkdays)}
            </DataTable.Summary.Cell>
            <DataTable.Summary.Cell index={2} align="right">
              {formatNumber(report.totalSundayWorkdays)}
            </DataTable.Summary.Cell>
            <DataTable.Summary.Cell index={3} align="right">
              {formatNumber(report.totalWorkdays)}
            </DataTable.Summary.Cell>
            <DataTable.Summary.Cell index={4} align="right">
              {formatNumber(report.totalOvertimeHours)}
            </DataTable.Summary.Cell>
          </DataTable.Summary.Row>
        </DataTable.Summary>
      )}
    />
  );
}
