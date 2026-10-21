import { useState } from 'react';
import { Alert, Button, Card, Descriptions, Modal, Space, Table, Tag, Typography, Upload } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { DownloadOutlined, InboxOutlined, UploadOutlined } from '@ant-design/icons';
import { MESSAGES } from '@/constants/messages';
import { MONTHLY_COST_ROW_STATUS_LABEL, MONTHLY_COST_TEXT } from '../constants';
import {
  useConfirmMonthlyCostImport,
  useDownloadMonthlyCostTemplate,
  usePreviewMonthlyCostImport,
} from '../hooks/useMonthlyCost';
import type { MonthlyCostExcelRowPreview } from '../types';
import { formatCurrency } from '../utils';

type Props = {
  periodMonth: string;
  /** Tháng đã khóa thì không cho nhập lại (xem MonthlyCostService.ImportAsync, MSG21). */
  disabled: boolean;
};

const columns: ColumnsType<MonthlyCostExcelRowPreview> = [
  { title: MONTHLY_COST_TEXT.previewColumns.rowNumber, dataIndex: 'rowNumber', width: 64 },
  {
    title: MONTHLY_COST_TEXT.previewColumns.costCategoryCode,
    dataIndex: 'costCategoryCode',
    render: (v: string | null) => v ?? '—',
  },
  { title: MONTHLY_COST_TEXT.previewColumns.costType, dataIndex: 'costType', render: (v: string | null) => v ?? '—' },
  {
    title: MONTHLY_COST_TEXT.previewColumns.productCode,
    dataIndex: 'productCode',
    render: (v: string | null) => v ?? '—',
  },
  {
    title: MONTHLY_COST_TEXT.previewColumns.productName,
    dataIndex: 'productName',
    render: (v: string | null) => v ?? '—',
  },
  {
    title: MONTHLY_COST_TEXT.previewColumns.amount,
    dataIndex: 'amount',
    align: 'right',
    render: (v: number | null) => (v === null ? '—' : formatCurrency(v)),
  },
  {
    title: MONTHLY_COST_TEXT.previewColumns.quantity,
    dataIndex: 'quantity',
    align: 'right',
    render: (v: number | null) => v ?? '—',
  },
  { title: MONTHLY_COST_TEXT.previewColumns.note, dataIndex: 'note', render: (v: string | null) => v ?? '—' },
  {
    title: MONTHLY_COST_TEXT.previewColumns.status,
    dataIndex: 'rowStatus',
    render: (v: keyof typeof MONTHLY_COST_ROW_STATUS_LABEL) => (
      <Tag color={MONTHLY_COST_ROW_STATUS_LABEL[v].color}>{MONTHLY_COST_ROW_STATUS_LABEL[v].text}</Tag>
    ),
  },
  {
    title: MONTHLY_COST_TEXT.previewColumns.errorMessage,
    dataIndex: 'errorMessage',
    render: (v: string | null) => v ?? '—',
  },
];

/**
 * UC12 — nhập chi phí tháng từ Excel: tải file mẫu, xem trước (MSG24, hợp lệ/lỗi/trùng), đối soát MEALS
 * rồi mới xác nhận ghi DB, cùng kiểu luồng với import bảng lương (DEV-MI-09-1).
 */
export function MonthlyCostImportPanel({ periodMonth, disabled }: Props) {
  const [file, setFile] = useState<File | null>(null);
  const downloadTemplate = useDownloadMonthlyCostTemplate();
  const preview = usePreviewMonthlyCostImport(periodMonth);
  const confirmImport = useConfirmMonthlyCostImport(periodMonth);

  const reset = () => {
    setFile(null);
    preview.reset();
  };

  const handleConfirm = () => {
    if (!file) return;
    Modal.confirm({
      title: MESSAGES.monthlyCost.confirmImportTitle,
      content: MESSAGES.monthlyCost.confirmImportContent,
      okText: MESSAGES.common.confirm,
      cancelText: MESSAGES.common.cancel,
      onOk: () => confirmImport.mutate(file, { onSuccess: reset }),
    });
  };

  return (
    <Card size="small" title={MONTHLY_COST_TEXT.import}>
      <Space direction="vertical" style={{ width: '100%' }} size={16}>
        {disabled ? <Alert type="warning" showIcon message={MESSAGES.monthlyCost.importDisabledHint} /> : null}

        <Button icon={<DownloadOutlined />} loading={downloadTemplate.isPending} onClick={() => downloadTemplate.mutate()}>
          {MONTHLY_COST_TEXT.downloadTemplate}
        </Button>

        <Upload.Dragger
          disabled={disabled}
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
          <p>{MONTHLY_COST_TEXT.chooseFile}</p>
        </Upload.Dragger>

        <Space>
          <Button
            icon={<UploadOutlined />}
            disabled={!file || disabled}
            loading={preview.isPending}
            onClick={() => file && preview.mutate(file)}
          >
            {MONTHLY_COST_TEXT.preview}
          </Button>
          <Button
            type="primary"
            disabled={!preview.data || preview.data.validCount === 0 || disabled}
            loading={confirmImport.isPending}
            onClick={handleConfirm}
          >
            {MONTHLY_COST_TEXT.import}
          </Button>
        </Space>

        {preview.data ? (
          <>
            <Descriptions column={3} size="small" bordered>
              <Descriptions.Item label={MONTHLY_COST_TEXT.previewSummary.valid}>
                <Typography.Text type="success">{preview.data.validCount}</Typography.Text>
              </Descriptions.Item>
              <Descriptions.Item label={MONTHLY_COST_TEXT.previewSummary.error}>
                <Typography.Text type="danger">{preview.data.errorCount}</Typography.Text>
              </Descriptions.Item>
              <Descriptions.Item label={MONTHLY_COST_TEXT.previewSummary.duplicate}>
                <Typography.Text type="warning">{preview.data.duplicateCount}</Typography.Text>
              </Descriptions.Item>
            </Descriptions>

            {preview.data.mealsReconciliation.importedAmount !== null ? (
              <Alert
                type={preview.data.mealsReconciliation.hasDiscrepancy ? 'warning' : 'success'}
                showIcon
                message={MONTHLY_COST_TEXT.reconciliation.title}
                description={`${MONTHLY_COST_TEXT.reconciliation.expected}: ${formatCurrency(preview.data.mealsReconciliation.expectedAmount)} — ${MONTHLY_COST_TEXT.reconciliation.imported}: ${formatCurrency(preview.data.mealsReconciliation.importedAmount)}. ${preview.data.mealsReconciliation.hasDiscrepancy ? MONTHLY_COST_TEXT.reconciliation.mismatch : MONTHLY_COST_TEXT.reconciliation.match}`}
              />
            ) : null}

            <Table<MonthlyCostExcelRowPreview>
              rowKey="rowNumber"
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
