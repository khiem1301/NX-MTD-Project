import { useState } from 'react';
import { Alert, Button, Card, Descriptions, Space, Typography, Upload } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { InboxOutlined, UploadOutlined } from '@ant-design/icons';
import { MESSAGES } from '@/constants/messages';
import { ATTENDANCE_TEXT } from '../constants';
import { useConfirmExcelImport, usePreviewExcelImport } from '../hooks/useAttendance';
import type { AttendanceExcelPreviewRow } from '../types';
import { confirmDialog } from '@/utils/confirmDialog';
import { formatDate } from '@/utils/format';
import { DataTable } from '@/components/common/DataTable';

const columns: ColumnsType<AttendanceExcelPreviewRow> = [
  {
    title: ATTENDANCE_TEXT.import.previewColumns.attendanceCode,
    dataIndex: 'attendanceCode',
    width: 140,
  },
  {
    title: ATTENDANCE_TEXT.import.previewColumns.workDate,
    dataIndex: 'workDate',
    render: (v: string) => formatDate(v),
  },
  {
    title: ATTENDANCE_TEXT.import.previewColumns.checkIn,
    dataIndex: 'checkIn',
    render: (v: string | null) => v ?? '—',
  },
  {
    title: ATTENDANCE_TEXT.import.previewColumns.checkOut,
    dataIndex: 'checkOut',
    render: (v: string | null) => v ?? '—',
  },
];

/**
 * UC29 — nhập file Excel chấm công dự phòng khi không đọc được Ronald Jack Pro (JOB-01 lỗi, MSG23).
 * Cùng luồng xem trước (chưa ghi DB) rồi mới xác nhận như import bảng lương (DEV-MI-09-1).
 */
export function ExcelImportPanel() {
  const [file, setFile] = useState<File | null>(null);
  const preview = usePreviewExcelImport();
  const confirmImport = useConfirmExcelImport();

  const reset = () => {
    setFile(null);
    preview.reset();
  };

  const handleConfirm = () => {
    if (!file) return;
    confirmDialog({
      title: ATTENDANCE_TEXT.import.confirmTitle,
      content: ATTENDANCE_TEXT.import.confirmContent,
      okText: MESSAGES.common.confirm,
      cancelText: MESSAGES.common.cancel,
      onOk: () => confirmImport.mutate(file, { onSuccess: reset }),
    });
  };

  return (
    <Card size="small" title={ATTENDANCE_TEXT.import.title}>
      <Space direction="vertical" style={{ width: '100%' }} size={16}>
        <Alert type="info" showIcon message={ATTENDANCE_TEXT.import.hint} />

        <Upload.Dragger
          maxCount={1}
          accept=".xlsx,.xls"
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
          <p>{ATTENDANCE_TEXT.import.chooseFile}</p>
        </Upload.Dragger>

        <Space>
          <Button
            icon={<UploadOutlined />}
            disabled={!file}
            loading={preview.isPending}
            onClick={() => file && preview.mutate(file)}
          >
            {ATTENDANCE_TEXT.import.preview}
          </Button>
          <Button
            type="primary"
            disabled={!preview.data || preview.data.punchCount === 0}
            loading={confirmImport.isPending}
            onClick={handleConfirm}
          >
            {ATTENDANCE_TEXT.import.confirm}
          </Button>
        </Space>

        {preview.data ? (
          <>
            <Descriptions column={2} size="small" bordered>
              <Descriptions.Item label={ATTENDANCE_TEXT.import.previewSummary.rowCount}>
                {preview.data.rowCount}
              </Descriptions.Item>
              <Descriptions.Item label={ATTENDANCE_TEXT.import.previewSummary.punchCount}>
                <Typography.Text type="success">{preview.data.punchCount}</Typography.Text>
              </Descriptions.Item>
            </Descriptions>
            <DataTable<AttendanceExcelPreviewRow>
              rowKey={(row) =>
                `${row.attendanceCode}-${row.workDate}-${row.checkIn ?? ''}-${row.checkOut ?? ''}`
              }
              size="small"
              columns={columns}
              dataSource={preview.data.rows}
              pagination={{ pageSize: 10 }}
              scroll={{ x: 'max-content' }}
            />
          </>
        ) : null}
      </Space>
    </Card>
  );
}
