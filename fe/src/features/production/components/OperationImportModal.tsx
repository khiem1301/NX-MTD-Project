import { useState } from 'react';
import { Alert, Descriptions, Modal, Select, Space, Table, Typography, Upload, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { InboxOutlined } from '@ant-design/icons';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { fill, formatQty } from '@/utils/format';
import { PRODUCTION_TEXT } from '../constants';
import { useImportOperations, usePreviewOperationImport } from '../hooks/useProduction';
import type { OperationImportRow } from '../types';

type Props = { open: boolean; productId: number; productLabel: string; onClose: () => void };

const text = PRODUCTION_TEXT.operations.import;

const columns: ColumnsType<OperationImportRow> = [
  { title: text.columns.stt, dataIndex: 'stt', width: 70 },
  { title: text.columns.code, dataIndex: 'operationCode', width: 90 },
  { title: text.columns.name, dataIndex: 'operationName', render: (v: string | null) => v ?? '—' },
  { title: text.columns.skill, dataIndex: 'skillLevel', width: 70, render: (v: number | null) => v ?? '—' },
  {
    title: text.columns.time,
    dataIndex: 'standardTimeSec',
    align: 'right',
    render: (v: number | null) => (v === null ? '—' : formatQty(v)),
  },
  { title: text.columns.equipment, dataIndex: 'equipment', render: (v: string | null) => v ?? '—' },
  {
    title: text.columns.action,
    key: 'action',
    render: (_, row) => (
      <Space direction="vertical" size={0}>
        <StatusTag status={row.action} labels={text.action} />
        {row.error && <Typography.Text type="danger">{row.error}</Typography.Text>}
      </Space>
    ),
  },
];

/**
 * KI-01 (UC25) — Kế toán nhập công đoạn của một mã hàng từ file Excel quy trình công nghệ: chọn file là xem trước ngay
 * (thêm mới / cập nhật / không đổi / lỗi), chọn sheet nếu file có nhiều bảng, có thể chọn công đoạn cuối rồi mới ghi.
 * Trang chỉ gắn hộp thoại khi đang mở nên state tự reset mỗi lần mở.
 */
export function OperationImportModal({ open, productId, productLabel, onClose }: Props) {
  const [file, setFile] = useState<File | null>(null);
  const [finalStt, setFinalStt] = useState<number | undefined>(undefined);
  const preview = usePreviewOperationImport();
  const importOps = useImportOperations();
  const data = preview.data;

  const runPreview = (nextFile: File, sheetName?: string) => {
    setFinalStt(undefined);
    preview.mutate({ file: nextFile, productId, sheetName });
  };

  const handleConfirm = () => {
    if (!file || !data) return;
    Modal.confirm({
      title: text.confirmTitle,
      content: text.confirmContent,
      okText: MESSAGES.common.confirm,
      cancelText: MESSAGES.common.cancel,
      onOk: () =>
        importOps.mutateAsync({ file, params: { productId, sheetName: data.sheetName, finalStt } }).then((result) => {
          void message.success(fill(text.resultTemplate, result.created, result.updated, result.unchanged, result.skipped));
          onClose();
        }),
    });
  };

  const importable = (data?.newCount ?? 0) + (data?.updateCount ?? 0) + (finalStt !== undefined ? 1 : 0);
  const finalOptions = (data?.rows ?? [])
    .filter((r) => r.action !== 'Error')
    .map((r) => ({ value: r.stt, label: `${r.stt}. ${r.operationName ?? ''}` }));

  return (
    <Modal
      open={open}
      width={920}
      title={`${text.title} — ${productLabel}`}
      okText={text.confirm}
      cancelText={MESSAGES.common.cancel}
      okButtonProps={{ disabled: !data || importable === 0, loading: importOps.isPending }}
      onOk={handleConfirm}
      onCancel={onClose}
      destroyOnHidden
    >
      <Space direction="vertical" size={16} style={{ width: '100%' }}>
        <Typography.Text type="secondary">{text.hint}</Typography.Text>

        <Upload.Dragger
          maxCount={1}
          accept=".xlsx"
          disabled={preview.isPending}
          beforeUpload={(nextFile) => {
            setFile(nextFile);
            runPreview(nextFile);
            return false;
          }}
          onRemove={() => {
            setFile(null);
            setFinalStt(undefined);
            preview.reset();
          }}
        >
          <p className="ant-upload-drag-icon">
            <InboxOutlined />
          </p>
          <p>{text.chooseFile}</p>
          <p className="ant-upload-hint">{text.chooseFileHint}</p>
        </Upload.Dragger>

        {preview.error && <Alert type="error" showIcon message={preview.error.message} />}

        {data && file && (
          <>
            {data.productText && (
              <Alert type="info" showIcon message={`${text.productInFile}: ${data.productText}`} description={text.productCheck} />
            )}

            <Space wrap size={24}>
              {data.sheetNames.length > 1 && (
                <Space>
                  <span>{text.sheet}</span>
                  <Select
                    style={{ width: 220 }}
                    value={data.sheetName}
                    options={data.sheetNames.map((s) => ({ value: s, label: s }))}
                    onChange={(sheetName) => runPreview(file, sheetName)}
                  />
                </Space>
              )}
              <Space>
                <span>{text.finalStep}</span>
                <Select
                  allowClear
                  showSearch
                  optionFilterProp="label"
                  style={{ width: 340 }}
                  placeholder={data.productHasFinalStep ? text.finalStepHasOne : text.finalStepPlaceholder}
                  disabled={data.productHasFinalStep}
                  value={finalStt}
                  options={finalOptions}
                  onChange={(v) => setFinalStt(v)}
                />
              </Space>
            </Space>

            <Descriptions column={4} size="small" bordered>
              <Descriptions.Item label={text.summary.created}>
                <Typography.Text type="success">{data.newCount}</Typography.Text>
              </Descriptions.Item>
              <Descriptions.Item label={text.summary.updated}>{data.updateCount}</Descriptions.Item>
              <Descriptions.Item label={text.summary.unchanged}>{data.unchangedCount}</Descriptions.Item>
              <Descriptions.Item label={text.summary.error}>
                <Typography.Text type={data.errorCount > 0 ? 'danger' : undefined}>{data.errorCount}</Typography.Text>
              </Descriptions.Item>
            </Descriptions>

            <Table<OperationImportRow>
              rowKey={(r) => `${r.rowNumber}-${r.stt}`}
              size="small"
              columns={columns}
              dataSource={data.rows}
              pagination={{ pageSize: 10, showSizeChanger: false }}
              scroll={{ x: 'max-content' }}
            />
          </>
        )}
      </Space>
    </Modal>
  );
}
