import { Alert, Checkbox, Space, Typography } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { AppDatePicker } from '@/components/common/DateInputs';
import { PRODUCTION_TEXT } from '../constants';
import type { OperationImportPreview } from '../types';

const text = PRODUCTION_TEXT.operations.import;

type OperationImportRateOptionsProps = {
  data: OperationImportPreview;
  withRates: boolean;
  effectiveDate: Dayjs;
  submitRates: boolean;
  onWithRatesChange: (checked: boolean) => void;
  onEffectiveDateChange: (date: Dayjs) => void;
  onSubmitRatesChange: (checked: boolean) => void;
};

/** Tùy chọn nhập đơn giá cùng file: có nhập hay không, ngày hiệu lực và có gửi Admin duyệt luôn không. */
export function OperationImportRateOptions({
  data,
  withRates,
  effectiveDate,
  submitRates,
  onWithRatesChange,
  onEffectiveDateChange,
  onSubmitRatesChange,
}: OperationImportRateOptionsProps) {
  return data.hasRateColumn ? (
    <Space direction="vertical" size={8}>
      <Checkbox
        checked={withRates}
        onChange={(e) => {
          onWithRatesChange(e.target.checked);
        }}
      >
        {text.withRates}
      </Checkbox>
      {withRates && (
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
        </Space>
      )}
      {withRates && <Typography.Text type="secondary">{text.rateHint}</Typography.Text>}
    </Space>
  ) : (
    <Alert type="warning" showIcon message={text.noRateColumn} />
  );
}
