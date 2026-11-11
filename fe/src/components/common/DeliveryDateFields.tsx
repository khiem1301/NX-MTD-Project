import { Form, Space } from 'antd';
import type { Dayjs } from 'dayjs';
import { AppDatePicker } from '@/components/common/DateInputs';
import { DELIVERY_TEXT } from '@/constants/delivery';
import { isExpectedDeliveryDateDisabled, isRangeDelivery } from '@/utils/expectedDelivery';

type DeliveryDateFieldsProps = {
  /** Số lần giao đang nhập: từ 2 trở lên thì hiện thêm ô "Giao đến ngày". */
  count: number | null | undefined;
  /** Nhãn ô ngày khi giao một lần (giao nhiều lần thì là "Giao từ ngày"). */
  label: string;
  requiredMessage: string;
  required?: boolean;
  disabled?: boolean;
  /** Không chọn được ngày trước ngày này (VD ngày đặt đơn mua). */
  notBefore?: Dayjs | null;
};

const FROM = 'expectedDeliveryDate';
const TO = 'expectedDeliveryDateTo';

/**
 * Ngày dự kiến giao trong một Form (field `expectedDeliveryDate`, `expectedDeliveryDateTo`): giao một lần nhập một ngày, giao từ
 * 2 lần trở lên nhập khoảng ngày (ngày đến không trước ngày từ). BE kiểm lại (DEV-ALL-94).
 */
export function DeliveryDateFields({
  count,
  label,
  requiredMessage,
  required = true,
  disabled,
  notBefore,
}: DeliveryDateFieldsProps) {
  const form = Form.useFormInstance();
  const from = Form.useWatch<Dayjs | null | undefined>(FROM, form);
  const range = isRangeDelivery(count);

  return (
    <Space wrap align="start">
      <Form.Item
        name={FROM}
        label={range ? DELIVERY_TEXT.dateFrom : label}
        rules={[{ required, message: requiredMessage }]}
      >
        <AppDatePicker
          disabled={disabled}
          disabledDate={(d) => isExpectedDeliveryDateDisabled(d, notBefore)}
        />
      </Form.Item>
      {range && (
        <Form.Item
          name={TO}
          label={DELIVERY_TEXT.dateTo}
          dependencies={[FROM]}
          rules={[
            { required: !disabled, message: DELIVERY_TEXT.dateToRequired },
            {
              validator: async (_rule, value?: Dayjs | null) => {
                if (value && from && value.isBefore(from, 'day'))
                  throw new Error(DELIVERY_TEXT.dateToBeforeFrom);
              },
            },
          ]}
        >
          <AppDatePicker
            disabled={disabled}
            disabledDate={(d) => isExpectedDeliveryDateDisabled(d, from ?? notBefore)}
          />
        </Form.Item>
      )}
    </Space>
  );
}
