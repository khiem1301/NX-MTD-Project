import { useState } from 'react';
import { InboxOutlined } from '@ant-design/icons';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Modal, Space, Table, Tag, Typography, Upload, message } from 'antd';
import { API } from '@/constants/apiEndpoints';
import { MESSAGES } from '@/constants/messages';
import { useRolePath } from '@/hooks/useRolePath';
import { apiClient } from '@/services/apiClient';
import type { ApiError, ApiResponse } from '@/types/api';

type ImportRow = {
  rowNumber: number;
  code: string | null;
  fullName: string | null;
  role: string | null;
  teamCode: string | null;
  status: 'VALID' | 'ERROR' | 'DUPLICATE';
  message: string | null;
};
type Preview = {
  validCount: number;
  errorCount: number;
  duplicateCount: number;
  summary: string;
  rows: ImportRow[];
};
type Created = { username: string; fullName: string; temporaryPassword: string };
type Result = {
  createdCount: number;
  failedCount: number;
  created: Created[];
  failed: ImportRow[];
};

const HEADERS =
  'Mã NV | Họ tên | Giới tính | Ngày sinh | SĐT | Vai trò | Mã tổ | Hình thức lương | Ngày vào làm | Mã chấm công';
const STATUS_TAG = {
  VALID: { color: 'green', text: 'Hợp lệ' },
  ERROR: { color: 'red', text: 'Lỗi' },
  DUPLICATE: { color: 'orange', text: 'Trùng' },
} as const;

const upload = async <T,>(url: string, file: File): Promise<T> => {
  const form = new FormData();
  form.append('file', file);
  return (await apiClient.post<ApiResponse<T>>(url, form)).data.data;
};

/**
 * KH-22 (UC17 import): Admin chọn file Excel nhân viên → xem trước 3 loại hợp lệ / lỗi / trùng (MSG24) → nhập các dòng hợp lệ.
 * Mật khẩu tạm của người vừa nhập chỉ hiện một lần trong hộp này (D29): không lưu store, đóng hộp là mất.
 */
export function EmployeeImportModal({ open, onClose }: { open: boolean; onClose: () => void }) {
  const queryClient = useQueryClient();
  const BASE = API.employees.import(useRolePath());
  const [file, setFile] = useState<File | null>(null);
  const preview = useMutation<Preview, ApiError, File>({
    mutationFn: (f) => upload<Preview>(`${BASE}/preview`, f),
    onError: (e) => void message.error(e.message),
  });
  const run = useMutation<Result, ApiError, File>({
    mutationFn: (f) => upload<Result>(BASE, f),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['employees'] }),
        queryClient.invalidateQueries({ queryKey: ['users'] }),
      ]);
    },
    onError: (e) => void message.error(e.message),
  });
  const data = preview.data;
  const result = run.data;

  const copyAll = async () => {
    if (!result) return;
    await navigator.clipboard.writeText(
      result.created.map((c) => `${c.username}\t${c.temporaryPassword}`).join('\n'),
    );
    void message.success(MESSAGES.hr.copied);
  };

  return (
    <Modal
      open={open}
      width={920}
      title="Nhập nhân viên từ Excel"
      onCancel={onClose}
      destroyOnHidden
      footer={
        result ? (
          <Button type="primary" onClick={onClose}>
            {MESSAGES.common.close}
          </Button>
        ) : (
          <Space>
            <Button onClick={onClose}>{MESSAGES.common.cancel}</Button>
            <Button
              type="primary"
              loading={run.isPending}
              disabled={!file || !data || data.validCount === 0}
              onClick={() =>
                file &&
                Modal.confirm({
                  title: 'Nhập các dòng hợp lệ?',
                  content: `${data?.validCount ?? 0} nhân viên sẽ được tạo kèm tài khoản và mật khẩu tạm.`,
                  okText: MESSAGES.common.confirm,
                  cancelText: MESSAGES.common.cancel,
                  onOk: () => run.mutateAsync(file),
                })
              }
            >
              Nhập {data?.validCount ?? 0} dòng hợp lệ
            </Button>
          </Space>
        )
      }
    >
      {!result && (
        <>
          <Alert
            type="info"
            showIcon
            style={{ marginBottom: 12 }}
            message={`Dòng đầu là tiêu đề, các cột theo thứ tự: ${HEADERS}.`}
            description="Vai trò: Worker, TeamLeader, TeamDeputy, QaQc, Electrician, WarehouseKeeper, Accountant, Manager, Admin. Hình thức lương: PieceRate / Daily / Monthly. Ngày: dd/MM/yyyy."
          />
          <Upload.Dragger
            accept=".xlsx"
            maxCount={1}
            beforeUpload={(f) => {
              setFile(f);
              preview.mutate(f);
              return false;
            }}
            onRemove={() => {
              setFile(null);
              preview.reset();
            }}
            style={{ marginBottom: 16 }}
          >
            <p className="ant-upload-drag-icon">
              <InboxOutlined />
            </p>
            <p>Chọn hoặc kéo thả file .xlsx</p>
          </Upload.Dragger>
          {data && (
            <>
              <Alert
                type={data.errorCount + data.duplicateCount > 0 ? 'warning' : 'success'}
                showIcon
                message={data.summary}
                style={{ marginBottom: 12 }}
              />
              <Table<ImportRow>
                rowKey="rowNumber"
                size="small"
                dataSource={data.rows}
                pagination={{ pageSize: 10, hideOnSinglePage: true }}
                columns={[
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
                        {r.message && (
                          <Typography.Text type="secondary">{r.message}</Typography.Text>
                        )}
                      </Space>
                    ),
                  },
                ]}
              />
            </>
          )}
        </>
      )}
      {result && (
        <>
          <Alert
            type={result.failedCount > 0 ? 'warning' : 'success'}
            showIcon
            style={{ marginBottom: 12 }}
            message={`Đã nhập ${result.createdCount} nhân viên, ${result.failedCount} dòng không nhập được.`}
            description="Mật khẩu tạm chỉ hiện một lần ở đây. Hãy sao chép và gửi cho từng người; đóng hộp này là không xem lại được."
            action={<Button onClick={() => void copyAll()}>Sao chép tất cả</Button>}
          />
          <Table<Created>
            rowKey="username"
            size="small"
            dataSource={result.created}
            pagination={{ pageSize: 10, hideOnSinglePage: true }}
            columns={[
              { title: 'Tên đăng nhập', dataIndex: 'username' },
              { title: 'Họ tên', dataIndex: 'fullName' },
              {
                title: 'Mật khẩu tạm',
                dataIndex: 'temporaryPassword',
                render: (v: string) => <Typography.Text code>{v}</Typography.Text>,
              },
            ]}
          />
          {result.failed.length > 0 && (
            <Table<ImportRow>
              style={{ marginTop: 12 }}
              rowKey="rowNumber"
              size="small"
              dataSource={result.failed}
              pagination={false}
              columns={[
                { title: 'Dòng không nhập', dataIndex: 'rowNumber' },
                { title: 'Mã NV', dataIndex: 'code', render: (v: string | null) => v ?? '—' },
                { title: 'Lý do', dataIndex: 'message', render: (v: string | null) => v ?? '—' },
              ]}
            />
          )}
        </>
      )}
    </Modal>
  );
}
