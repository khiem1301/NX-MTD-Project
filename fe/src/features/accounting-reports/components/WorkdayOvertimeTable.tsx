import {} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { ACCOUNTING_REPORT_TEXT } from '../constants';
import type { WorkdayOvertimeReport, WorkdayOvertimeRow, WorkerOperationOutput } from '../types';
import { formatNumber, formatQty } from '@/utils/format';
import { DataTable } from '@/components/common/DataTable';

type Props = { report: WorkdayOvertimeReport };

const operationColumns: ColumnsType<WorkerOperationOutput> = [
  {
    title: ACCOUNTING_REPORT_TEXT.workdayTable.operation,
    key: 'operation',
    render: (_, o) => `${o.operationCode} - ${o.operationName}`,
  },
  {
    title: ACCOUNTING_REPORT_TEXT.workdayTable.outputQty,
    dataIndex: 'outputQty',
    align: 'right',
    render: (v: number) => formatQty(v),
  },
  {
    title: ACCOUNTING_REPORT_TEXT.workdayTable.unitRate,
    dataIndex: 'unitRate',
    align: 'right',
    render: (v: number) => formatNumber(v),
  },
  {
    title: ACCOUNTING_REPORT_TEXT.workdayTable.outputAmount,
    dataIndex: 'amount',
    align: 'right',
    render: (v: number) => formatNumber(v),
  },
];

const columns: ColumnsType<WorkdayOvertimeRow> = [
  { title: ACCOUNTING_REPORT_TEXT.workdayTable.employeeCode, dataIndex: 'employeeCode' },
  { title: ACCOUNTING_REPORT_TEXT.workdayTable.fullName, dataIndex: 'fullName' },
  {
    title: ACCOUNTING_REPORT_TEXT.workdayTable.teamCode,
    dataIndex: 'teamCode',
    render: (v: string | null) => v ?? '—',
  },
  {
    title: ACCOUNTING_REPORT_TEXT.workdayTable.gradeCode,
    dataIndex: 'gradeCode',
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
    title: ACCOUNTING_REPORT_TEXT.workdayTable.outputQty,
    dataIndex: 'outputQty',
    align: 'right',
    render: (v: number) => formatQty(v),
  },
  {
    title: ACCOUNTING_REPORT_TEXT.workdayTable.outputAmount,
    dataIndex: 'outputAmount',
    align: 'right',
    render: (v: number) => formatNumber(v),
  },
  {
    title: ACCOUNTING_REPORT_TEXT.workdayTable.defectQty,
    dataIndex: 'defectQty',
    align: 'right',
    render: (v: number) => formatQty(v),
  },
];

/** UC36 — công, bậc thợ, sản lượng theo công đoạn × đơn giá, số lượng lỗi theo nhân viên trong tháng (DEV-ALL-199, 200); bấm mũi tên xem từng công đoạn. */
export function WorkdayOvertimeTable({ report }: Props) {
  return (
    <DataTable<WorkdayOvertimeRow>
      rowKey="employeeId"
      size="small"
      columns={columns}
      dataSource={report.rows}
      scroll={{ x: 'max-content' }}
      expandable={{
        rowExpandable: (row) => row.operations.length > 0,
        expandedRowRender: (row) => (
          <DataTable<WorkerOperationOutput>
            rowKey={(o) => `${o.operationCode}-${o.unitRate}`}
            size="small"
            columns={operationColumns}
            dataSource={row.operations}
            pagination={false}
          />
        ),
      }}
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
              {formatQty(report.totalOutputQty)}
            </DataTable.Summary.Cell>
            <DataTable.Summary.Cell index={5} align="right">
              {formatNumber(report.totalOutputAmount)}
            </DataTable.Summary.Cell>
            <DataTable.Summary.Cell index={6} align="right">
              {formatQty(report.totalDefectQty)}
            </DataTable.Summary.Cell>
          </DataTable.Summary.Row>
        </DataTable.Summary>
      )}
    />
  );
}
