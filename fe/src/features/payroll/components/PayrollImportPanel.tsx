import { useState } from 'react';
import { Alert, Button, Card, Descriptions, Modal, Space, Table, Tag, Typography, Upload } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { InboxOutlined, UploadOutlined } from '@ant-design/icons';
import { MESSAGES } from '@/constants/messages';
import { PAYROLL_ROW_STATUS_LABEL, PAYROLL_TEXT } from '../constants';
import { useConfirmImport, usePreviewImport } from '../hooks/usePayroll';
import type { PayrollExcelRowPreview } from '../types';
import { formatCurrency } from '../utils';

type Props = {
  periodMonth: string;
  /** Tháng đang Chờ duyệt / Đã duyệt thì không cho nhập lại (xem PayrollService.ImportAsync). */
  disabled: boolean;
};

const columns: ColumnsType<PayrollExcelRowPreview> = [
  { title: PAYROLL_TEXT.previewColumns.sheetName, dataIndex: 'sheetName' },
  { title: PAYROLL_TEXT.previewColumns.rowNumber, dataIndex: 'rowNumber', width: 64 },
  {
    title: PAYROLL_TEXT.previewColumns.attendanceCode,
    dataIndex: 'attendanceCode',
    render: (v: string | null) => v ?? '—',
  },
  { title: PAYROLL_TEXT.previewColumns.employeeName, dataIndex: 'employeeName', render: (v: string | null) => v ?? '—' },
  {
    title: PAYROLL_TEXT.previewColumns.workDays,
    dataIndex: 'workDays',
    align: 'right',
    render: (v: number | null) => v ?? '—',
  },
  {
    title: PAYROLL_TEXT.previewColumns.netPay,
    dataIndex: 'netPay',
    align: 'right',
    render: (v: number | null) => (v === null ? '—' : formatCurrency(v)),
  },
  { title: PAYROLL_TEXT.previewColumns.itemCount, dataIndex: 'itemCount', align: 'right' },
  {
    title: PAYROLL_TEXT.previewColumns.status,
    dataIndex: 'rowStatus',
    render: (v: keyof typeof PAYROLL_ROW_STATUS_LABEL) => (
      <Tag color={PAYROLL_ROW_STATUS_LABEL[v].color}>{PAYROLL_ROW_STATUS_LABEL[v].text}</Tag>
    ),
  },
  {
    // Dòng Hợp lệ mà có ghi chú = cảnh báo không chặn (lệch thực lĩnh, khác bậc thợ).
    title: PAYROLL_TEXT.previewColumns.message,
    dataIndex: 'message',
    render: (v: string | null, row) =>
      v === null ? (
        '—'
      ) : (
        <Typography.Text type={row.rowStatus === 'Valid' ? 'warning' : undefined}>{v}</Typography.Text>
      ),
  },
];

/**
 * UC31 (DEV-MI-09-4) — nhập file "Bảng thanh toán lương" (một sheet mỗi tổ): xem trước theo nhân viên
 * (MSG24, hợp lệ/lỗi/trùng + cảnh báo) rồi mới xác nhận ghi DB; file gốc được BE lưu lại cho giám đốc xem.
 */
export function PayrollImportPanel({ periodMonth, disabled }: Props) {
  const [file, setFile] = useState<File | null>(null);
  const preview = usePreviewImport();
  const confirmImport = useConfirmImport(periodMonth);

  const reset = () => {
    setFile(null);
    preview.reset();
  };

  const handleConfirm = () => {
    if (!file) return;
    Modal.confirm({
      title: PAYROLL_TEXT.confirmImportTitle,
      content: PAYROLL_TEXT.confirmImportContent,
      okText: MESSAGES.common.confirm,
      cancelText: MESSAGES.common.cancel,
      onOk: () => confirmImport.mutate(file, { onSuccess: reset }),
    });
  };

  return (
    <Card size="small" title={PAYROLL_TEXT.import}>
      <Space direction="vertical" style={{ width: '100%' }} size={16}>
        {disabled ? (
          <Alert type="warning" showIcon message={PAYROLL_TEXT.importDisabledHint} />
        ) : null}

        <Upload.Dragger
          disabled={disabled}
          maxCount={1}
          accept=".xlsx"
          beforeUpload={(nextFile) => {
            setFile(nextFile);
            preview.reset();
            return false;
          }}
          onRemove={reset}
        >
          <p className="ant-upload-drag-icon">
            <InboxOutlined />
          </p>
          <p>{PAYROLL_TEXT.chooseFile}</p>
          <p className="ant-upload-hint">{PAYROLL_TEXT.chooseFileHint}</p>
        </Upload.Dragger>

        <Space>
          <Button
            icon={<UploadOutlined />}
            disabled={!file || disabled}
            loading={preview.isPending}
            onClick={() => file && preview.mutate(file)}
          >
            {PAYROLL_TEXT.preview}
          </Button>
          <Button
            type="primary"
            disabled={!preview.data || preview.data.validCount === 0 || disabled}
            loading={confirmImport.isPending}
            onClick={handleConfirm}
          >
            {PAYROLL_TEXT.import}
          </Button>
        </Space>

        {preview.data ? (
          <>
            <Descriptions column={3} size="small" bordered>
              <Descriptions.Item label={PAYROLL_TEXT.previewSummary.valid}>
                <Typography.Text type="success">{preview.data.validCount}</Typography.Text>
              </Descriptions.Item>
              <Descriptions.Item label={PAYROLL_TEXT.previewSummary.error}>
                <Typography.Text type="danger">{preview.data.errorCount}</Typography.Text>
              </Descriptions.Item>
              <Descriptions.Item label={PAYROLL_TEXT.previewSummary.duplicate}>
                <Typography.Text type="warning">{preview.data.duplicateCount}</Typography.Text>
              </Descriptions.Item>
            </Descriptions>
            <Table<PayrollExcelRowPreview>
              rowKey={(row) => `${row.sheetName}-${row.rowNumber}`}
              size="small"
              columns={columns}
              dataSource={preview.data.rows}
              pagination={{ pageSize: 10 }}
              scroll={{ x: 'max-content' }}
              locale={{ emptyText: MESSAGES.common.empty }}
            />
          </>
        ) : null}
      </Space>
    </Card>
  );
}
