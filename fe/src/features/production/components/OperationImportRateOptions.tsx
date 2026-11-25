import { Alert, Checkbox, Space } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { AppDatePicker } from '@/components/common/DateInputs';
import { PRODUCTION_TEXT } from '../constants';
import type { OperationImportPreview } from '../types';
import { HintTip } from '@/components/common/HintTip';

const text = PRODUCTION_TEXT.operations.import;

type OperationImportRateOptionsProps = {
  data: OperationImportPreview;
  effectiveDate: Dayjs;
  submitRates: boolean;
  onEffectiveDateChange: (date: Dayjs) => void;
  onSubmitRatesChange: (checked: boolean) => void;
};

/**
 * Đơn giá bắt buộc với mọi công đoạn (DEV-ALL-190): nhập kèm từ cột TIỀN, dòng thiếu hoặc bằng 0 báo lỗi. Chỉ chọn ngày hiệu lực và
 * có gửi Admin duyệt luôn không. File không có cột TIỀN thì nhập tay ở cột Đơn giá của bảng.
 */
export function OperationImportRateOptions({
  data,
  effectiveDate,
  submitRates,
  onEffectiveDateChange,
  onSubmitRatesChange,
}: OperationImportRateOptionsProps) {
  return (
    <Space direction="vertical" size={8}>
      {!data.hasRateColumn && <Alert type="warning" showIcon message={text.noRateColumn} />}
      <Space wrap size={24}>
        <Space>
          <span>{text.effectiveDate}</span>
          <AppDatePicker
            allowClear={false}
            value={effectiveDate}
            onChange={(v) => {
              onEffectiveDateChange(v ?? dayjs());
            }}
          />
        </Space>
        <Checkbox checked={submitRates} onChange={(e) => onSubmitRatesChange(e.target.checked)}>
          {text.submitRates}
        </Checkbox>
        <HintTip text={text.rateHint} />
      </Space>
    </Space>
  );
}
