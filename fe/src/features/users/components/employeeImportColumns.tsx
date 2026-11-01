import { Space, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { ImportCreated, ImportRow } from '../hooks/useEmployeeImport';

const STATUS_TAG = {
  VALID: { color: 'green', text: 'Hợp lệ' },
  ERROR: { color: 'red', text: 'Lỗi' },
  DUPLICATE: { color: 'orange', text: 'Trùng' },
} as const;

/** Cột bảng xem trước file nhập nhân viên: dòng, mã, họ tên, vai trò, tổ, kết quả kiểm tra. */
export const previewColumns: ColumnsType<ImportRow> = [
  { title: 'Dòng', dataIndex: 'rowNumber', width: 60 },
  { title: 'Mã NV', dataIndex: 'code', render: (v: string | null) => v ?? '—' },
  {
    title: 'Họ tên',
    dataIndex: 'fullName',
    render: (v: string | null) => v ?? '—',
  },
  { title: 'Vai trò', dataIndex: 'role', render: (v: string | null) => v ?? '—' },
  { title: 'Tổ', dataIndex: 'teamCode', render: (v: string | null) => v ?? '—' },
  {
    title: 'Kết quả',
    key: 'status',
    render: (_, r) => (
      <Space direction="vertical" size={0}>
        <Tag color={STATUS_TAG[r.status].color}>{STATUS_TAG[r.status].text}</Tag>
        {r.message && <Typography.Text type="secondary">{r.message}</Typography.Text>}
      </Space>
    ),
  },
];

/** Cột bảng tài khoản vừa tạo: tên đăng nhập, họ tên, mật khẩu tạm. */
export const createdColumns: ColumnsType<ImportCreated> = [
  { title: 'Tên đăng nhập', dataIndex: 'username' },
  { title: 'Họ tên', dataIndex: 'fullName' },
  {
    title: 'Mật khẩu tạm',
    dataIndex: 'temporaryPassword',
    render: (v: string) => <Typography.Text code>{v}</Typography.Text>,
  },
];

/** Cột bảng các dòng không nhập được. */
export const failedColumns: ColumnsType<ImportRow> = [
  { title: 'Dòng không nhập', dataIndex: 'rowNumber' },
  { title: 'Mã NV', dataIndex: 'code', render: (v: string | null) => v ?? '—' },
  { title: 'Lý do', dataIndex: 'message', render: (v: string | null) => v ?? '—' },
];
