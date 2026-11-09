import { EyeOutlined } from '@ant-design/icons';

import type { ColumnsType } from 'antd/es/table';
import { IconAction } from '@/components/common/IconAction';
import { MESSAGES } from '@/constants/messages';
import { PAYROLL_TEXT } from '../constants';
import type { Payslip } from '../types';
import { formatCurrency } from '@/utils/format';

type PayslipColumnsOptions = {
  onView: (row: Payslip) => void;
};

/** Cột bảng phiếu lương trong chi tiết bảng lương: thông tin, các khoản cộng / trừ, thực nhận và nút xem phiếu. */
export const payslipColumns = ({ onView }: PayslipColumnsOptions): ColumnsType<Payslip> => [
  {
    title: PAYROLL_TEXT.table.employee,
    dataIndex: 'fullName',
    render: (v: string, row) => `${row.employeeCode} - ${v}`,
  },
  {
    title: PAYROLL_TEXT.table.team,
    dataIndex: 'teamName',
    render: (v: string | null) => v ?? '—',
  },
  {
    title: PAYROLL_TEXT.table.workDays,
    dataIndex: 'workDays',
    align: 'right',
    render: (v: number | null) => v ?? '—',
  },
  {
    title: PAYROLL_TEXT.table.totalAdditions,
    dataIndex: 'totalAdditions',
    align: 'right',
    render: (v: number) => formatCurrency(v),
  },
  {
    title: PAYROLL_TEXT.table.totalDeductions,
    dataIndex: 'totalDeductions',
    align: 'right',
    render: (v: number) => formatCurrency(v),
  },
  {
    title: PAYROLL_TEXT.table.insurance,
    dataIndex: 'insurance',
    align: 'right',
    render: (v: number) => formatCurrency(v),
  },
  {
    title: PAYROLL_TEXT.table.netPay,
    dataIndex: 'netPay',
    align: 'right',
    render: (v: number) => formatCurrency(v),
  },
  {
    title: MESSAGES.common.actions,
    key: 'action',
    render: (_, row) => (
      <IconAction title={MESSAGES.common.view} icon={<EyeOutlined />} onClick={() => onView(row)} />
    ),
  },
];
