import { Alert, Button, Descriptions, Modal, Space, Table, Typography } from 'antd';
import { DownloadOutlined, PlusOutlined, UndoOutlined } from '@ant-design/icons';
import { ImportFileBar } from '@/components/common/ImportFileBar';
import { HintTip } from '@/components/common/HintTip';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { MESSAGES } from '@/constants/messages';
import { formatCurrency } from '@/utils/format';
import { MONTHLY_COST_IMPORT_TEXT as text, MONTHLY_COST_TEXT } from '../constants';
import { useMonthlyCostImport } from '../hooks/useMonthlyCostImport';
import { useDownloadMonthlyCostTemplate } from '../hooks/useMonthlyCost';
import type { MonthlyCostDraftRow } from '../types';
import { monthlyCostImportColumns } from './monthlyCostImportColumns';

type Props = { open: boolean; periodMonth: string; onClose: () => void };

/**
 * DEV-ALL-187 — Kế toán nhập chi phí tháng từ Excel, thiết kế như nhập đơn / mã hàng: chọn file là xem trước ngay (hợp lệ / lỗi / trùng,
 * đối soát suất ăn), sửa được từng ô, thêm / xóa dòng, máy chủ kiểm lại sau mỗi lần sửa; nhập từ bảng đã sửa.
 * Trang chỉ gắn hộp thoại khi đang mở nên state tự reset mỗi lần mở.
 */
export function MonthlyCostImportModal({ open, periodMonth, onClose }: Props) {
  const imp = useMonthlyCostImport(periodMonth, onClose);
  const downloadTemplate = useDownloadMonthlyCostTemplate();
  const { data } = imp;
  const reconciliation = data?.mealsReconciliation;

  return (
    <Modal
      open={open}
      width={MODAL_WIDTH.lg}
      title={`${text.title} — ${periodMonth}`}
      okText={text.confirm}
      cancelText={MESSAGES.common.cancel}
      okButtonProps={{
        disabled: !data || imp.importable === 0 || imp.checking,
        loading: imp.importing,
      }}
      onOk={imp.handleConfirm}
      onCancel={onClose}
      destroyOnHidden
    >
      <Space direction="vertical" size={16} style={{ width: '100%' }}>
        <HintTip text={text.hint} />

        <ImportFileBar
          fileName={imp.preview.variables?.name}
          emptyText={text.chooseFile}
          hint={text.chooseFileHint}
          loading={imp.preview.isPending}
          onPick={(file) => imp.preview.mutate(file)}
          extra={
            <Button
              icon={<DownloadOutlined />}
              loading={downloadTemplate.isPending}
              onClick={() => downloadTemplate.mutate()}
            >
              {MONTHLY_COST_TEXT.downloadTemplate}
            </Button>
          }
        />

        {imp.preview.error && <Alert type="error" showIcon message={imp.preview.error.message} />}

        {data && (
          <>
            <Descriptions column={3} size="small" bordered>
              <Descriptions.Item label={MONTHLY_COST_TEXT.previewSummary.valid}>
                <Typography.Text type="success">{data.validCount}</Typography.Text>
              </Descriptions.Item>
              <Descriptions.Item label={MONTHLY_COST_TEXT.previewSummary.error}>
                <Typography.Text type="danger">{data.errorCount}</Typography.Text>
              </Descriptions.Item>
              <Descriptions.Item label={MONTHLY_COST_TEXT.previewSummary.duplicate}>
                <Typography.Text type="warning">{data.duplicateCount}</Typography.Text>
              </Descriptions.Item>
            </Descriptions>

            {reconciliation && reconciliation.importedAmount !== null && (
              <Alert
                type={reconciliation.hasDiscrepancy ? 'warning' : 'success'}
                showIcon
                message={MONTHLY_COST_TEXT.reconciliation.title}
                description={`${MONTHLY_COST_TEXT.reconciliation.expected}: ${formatCurrency(reconciliation.expectedAmount)} — ${MONTHLY_COST_TEXT.reconciliation.imported}: ${formatCurrency(reconciliation.importedAmount)}. ${reconciliation.hasDiscrepancy ? MONTHLY_COST_TEXT.reconciliation.mismatch : MONTHLY_COST_TEXT.reconciliation.match}`}
              />
            )}

            {imp.checkError && <Alert type="error" showIcon message={imp.checkError} />}
            <Space wrap>
              <Typography.Text type="secondary">
                {imp.checking ? text.checking : text.editHint}
              </Typography.Text>
              {imp.edited && (
                <Button size="small" icon={<UndoOutlined />} onClick={imp.reset}>
                  {text.resetEdits}
                </Button>
              )}
            </Space>
            <Table<MonthlyCostDraftRow>
              rowKey="rowNumber"
              size="small"
              pagination={false}
              columns={monthlyCostImportColumns({ data, update: imp.update, remove: imp.remove })}
              dataSource={imp.rows}
              scroll={{ x: 'max-content', y: 360 }}
              onRow={(r) => ({
                style:
                  data.rows.find((x) => x.rowNumber === r.rowNumber)?.rowStatus === 'Error'
                    ? { background: 'var(--ant-color-error-bg)' }
                    : undefined,
              })}
            />
            <Button icon={<PlusOutlined />} size="small" onClick={imp.add}>
              {text.addRow}
            </Button>
          </>
        )}
      </Space>
    </Modal>
  );
}
