import { useState } from 'react';
import { Alert, Descriptions, Modal, Space, Switch, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { ImportFileBar } from '@/components/common/ImportFileBar';
import { DataTable } from '@/components/common/DataTable';
import { HintTip } from '@/components/common/HintTip';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { ReasonHover } from '@/components/common/ReasonHover';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { confirmDialog } from '@/utils/confirmDialog';
import { formatCurrency } from '@/utils/format';
import { PAYROLL_ROW_STATUS_LABEL, PAYROLL_TEXT } from '../constants';
import { useConfirmImport, usePreviewImport } from '../hooks/usePayroll';
import type { PayrollExcelRowPreview } from '../types';

type Props = { open: boolean; periodMonth: string; onClose: () => void };

const text = PAYROLL_TEXT.previewColumns;

const columns: ColumnsType<PayrollExcelRowPreview> = [
  { title: text.sheetName, dataIndex: 'sheetName' },
  { title: text.rowNumber, dataIndex: 'rowNumber', width: 64 },
  {
    title: text.attendanceCode,
    dataIndex: 'attendanceCode',
    render: (v: string | null) => v ?? '—',
  },
  { title: text.employeeName, dataIndex: 'employeeName', render: (v: string | null) => v ?? '—' },
  {
    title: text.workDays,
    dataIndex: 'workDays',
    align: 'right',
    render: (v: number | null) => v ?? '—',
  },
  {
    title: text.netPay,
    dataIndex: 'netPay',
    align: 'right',
    render: (v: number | null) => (v === null ? '—' : formatCurrency(v)),
  },
  { title: text.itemCount, dataIndex: 'itemCount', align: 'right' },
  {
    // Dòng Hợp lệ mà có ghi chú = cảnh báo không chặn (lệch thực lĩnh, khác bậc thợ).
    title: text.status,
    key: 'status',
    fixed: 'right',
    width: 130,
    render: (_, r) => (
      <ReasonHover
        reasons={[
          r.message && { text: r.message, type: r.rowStatus === 'Valid' ? 'warning' : 'danger' },
        ]}
      >
        <StatusTag status={r.rowStatus} labels={PAYROLL_ROW_STATUS_LABEL} />
      </ReasonHover>
    ),
  },
];

/**
 * UC31 (DEV-MI-09-4, DEV-ALL-187) — Kế toán nhập file "Bảng thanh toán lương" (một sheet mỗi tổ) ở popup như nhập đơn / mã hàng:
 * chọn file là xem trước ngay theo nhân viên (hợp lệ / lỗi / trùng + cảnh báo, lý do hiện khi đưa chuột vào cột Kết quả), lỗi sai file
 * hiện ngay trong popup rồi mới xác nhận ghi DB. Bảng lương do xưởng tính sẵn (hệ thống không tính lại, GBR-12) nên không sửa ô tại đây:
 * sửa file rồi chọn lại.
 */
export function PayrollImportModal({ open, periodMonth, onClose }: Props) {
  const [file, setFile] = useState<File | null>(null);
  const [onlyProblems, setOnlyProblems] = useState(false);
  const preview = usePreviewImport();
  const confirmImport = useConfirmImport(periodMonth);
  const data = preview.data;
  const rows = (data?.rows ?? []).filter(
    (r) => !onlyProblems || r.rowStatus !== 'Valid' || r.message,
  );

  const handleConfirm = () =>
    file &&
    confirmDialog({
      title: PAYROLL_TEXT.confirmImportTitle,
      content: PAYROLL_TEXT.confirmImportContent,
      okText: MESSAGES.common.confirm,
      cancelText: MESSAGES.common.cancel,
      onOk: () => confirmImport.mutateAsync(file).then(() => onClose()),
    });

  return (
    <Modal
      open={open}
      width={MODAL_WIDTH.lg}
      title={`${PAYROLL_TEXT.importTitle} — ${periodMonth}`}
      okText={PAYROLL_TEXT.import}
      cancelText={MESSAGES.common.cancel}
      okButtonProps={{ disabled: !data || data.validCount === 0, loading: confirmImport.isPending }}
      onOk={handleConfirm}
      onCancel={onClose}
      destroyOnHidden
    >
      <Space direction="vertical" size={16} style={{ width: '100%' }}>
        <HintTip text={PAYROLL_TEXT.importHint} />
        <ImportFileBar
          fileName={file?.name}
          emptyText={PAYROLL_TEXT.chooseFile}
          hint={PAYROLL_TEXT.chooseFileHint}
          loading={preview.isPending}
          onPick={(next) => {
            setFile(next);
            preview.mutate(next);
          }}
        />

        {preview.error && <Alert type="error" showIcon message={preview.error.message} />}

        {data && (
          <>
            <Descriptions column={3} size="small" bordered>
              <Descriptions.Item label={PAYROLL_TEXT.previewSummary.valid}>
                <Typography.Text type="success">{data.validCount}</Typography.Text>
              </Descriptions.Item>
              <Descriptions.Item label={PAYROLL_TEXT.previewSummary.error}>
                <Typography.Text type="danger">{data.errorCount}</Typography.Text>
              </Descriptions.Item>
              <Descriptions.Item label={PAYROLL_TEXT.previewSummary.duplicate}>
                <Typography.Text type="warning">{data.duplicateCount}</Typography.Text>
              </Descriptions.Item>
            </Descriptions>
            <Space>
              <Switch checked={onlyProblems} onChange={setOnlyProblems} />
              <Typography.Text>{PAYROLL_TEXT.onlyProblems}</Typography.Text>
            </Space>
            <DataTable<PayrollExcelRowPreview>
              rowKey={(row) => `${row.sheetName}-${row.rowNumber}`}
              size="small"
              columns={columns}
              dataSource={rows}
              scroll={{ x: 'max-content', y: 360 }}
              onRow={(r) => ({
                style:
                  r.rowStatus === 'Error' ? { background: 'var(--ant-color-error-bg)' } : undefined,
              })}
            />
          </>
        )}
      </Space>
    </Modal>
  );
}
