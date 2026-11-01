import { Alert, Modal, Select, Space, Typography, Upload } from 'antd';
import { InboxOutlined } from '@ant-design/icons';
import { MESSAGES } from '@/constants/messages';
import { PRODUCTION_TEXT } from '../constants';
import type { OperationImportRow } from '../types';
import { DataTable } from '@/components/common/DataTable';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { useOperationImport } from '../hooks/useOperationImport';
import { operationImportColumns } from './operationImportColumns';
import { OperationImportRateOptions } from './OperationImportRateOptions';
import { OperationImportSummary } from './OperationImportSummary';

type Props = { open: boolean; productId: number; productLabel: string; onClose: () => void };

const text = PRODUCTION_TEXT.operations.import;

/**
 * KI-01 (UC25) — Kế toán nhập công đoạn của một mã hàng từ file Excel quy trình công nghệ: chọn file là xem trước ngay
 * (thêm mới / cập nhật / không đổi / lỗi), chọn sheet nếu file có nhiều bảng, chọn công đoạn cuối. Cột TIỀN được đọc
 * thành đơn giá công đoạn hiệu lực từ ngày chọn (Nháp, hoặc gửi Admin duyệt luôn).
 * Trang chỉ gắn hộp thoại khi đang mở nên state tự reset mỗi lần mở.
 */
export function OperationImportModal({ open, productId, productLabel, onClose }: Props) {
  const {
    file,
    setFile,
    finalStt,
    setFinalStt,
    withRates,
    setWithRates,
    effectiveDate,
    setEffectiveDate,
    submitRates,
    setSubmitRates,
    preview,
    importOps,
    data,
    ratesOn,
    runPreview,
    handleConfirm,
    importable,
    finalOptions,
  } = useOperationImport(productId, onClose);

  return (
    <Modal
      open={open}
      width={MODAL_WIDTH.lg}
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
            runPreview(nextFile, { sheetName: undefined });
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
              <Alert
                type="info"
                showIcon
                message={`${text.productInFile}: ${data.productText}`}
                description={text.productCheck}
              />
            )}

            <Space wrap size={24}>
              {data.sheetNames.length > 1 && (
                <Space>
                  <span>{text.sheet}</span>
                  <Select
                    style={{ width: 220 }}
                    value={data.sheetName}
                    options={data.sheetNames.map((s) => ({ value: s, label: s }))}
                    onChange={(sheetName) => runPreview(file, { sheetName })}
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
                  placeholder={
                    data.productHasFinalStep ? text.finalStepHasOne : text.finalStepPlaceholder
                  }
                  disabled={data.productHasFinalStep}
                  value={finalStt}
                  options={finalOptions}
                  onChange={(v) => setFinalStt(v)}
                />
              </Space>
            </Space>

            <OperationImportRateOptions
              data={data}
              withRates={withRates}
              effectiveDate={effectiveDate}
              submitRates={submitRates}
              onWithRatesChange={(checked) => {
                setWithRates(checked);
                runPreview(file, { withRates: checked });
              }}
              onEffectiveDateChange={(date) => {
                setEffectiveDate(date);
                runPreview(file, { date });
              }}
              onSubmitRatesChange={setSubmitRates}
            />

            <OperationImportSummary data={data} ratesOn={ratesOn} />

            <DataTable<OperationImportRow>
              rowKey={(r) => `${r.rowNumber}-${r.stt}`}
              size="small"
              columns={operationImportColumns}
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
