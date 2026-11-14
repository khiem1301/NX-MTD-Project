import { Form, Space } from 'antd';
import type { Dayjs } from 'dayjs';
import { AppDatePicker } from '@/components/common/DateInputs';
import { DELIVERY_TEXT } from '@/constants/delivery';
import { deliveryDateSlots, isExpectedDeliveryDateDisabled } from '@/utils/expectedDelivery';
import { fill } from '@/utils/format';

type DeliveryDateFieldsProps = {
  /** Số lần giao đang nhập: hiện đúng bấy nhiêu ô ngày, mỗi lần giao một ngày. */
  count: number | null | undefined;
  /** Nhãn ô ngày khi chỉ giao một lần (giao nhiều lần thì là "Ngày giao lần N"). */
  label: string;
  requiredMessage: string;
  required?: boolean;
  disabled?: boolean;
  /** Không chọn được ngày trước ngày này (VD ngày đặt đơn mua). */
  notBefore?: Dayjs | null;
};

/** Tên field mảng ngày (Dayjs) trong Form chứa component này; gửi API qua toDeliveryDatesPayload. */
export const DELIVERY_DATES_FIELD = 'expectedDeliveryDates';

/**
 * Ngày dự kiến giao trong một Form: số lần giao là bao nhiêu thì nhập bấy nhiêu ngày (không nhập khoảng), ngày sau không
 * trước ngày liền trước. BE kiểm lại (DEV-ALL-94).
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
  const dates = Form.useWatch<(Dayjs | null | undefined)[] | undefined>(DELIVERY_DATES_FIELD, form);
  const slots = deliveryDateSlots(count);

  return (
    <Space wrap align="start">
      {Array.from({ length: slots }, (_, i) => {
        const previous = i > 0 ? dates?.[i - 1] : undefined;
        return (
          <Form.Item
            key={i}
            name={[DELIVERY_DATES_FIELD, i]}
            label={slots > 1 ? fill(DELIVERY_TEXT.dateOfTurn, i + 1) : label}
            dependencies={i > 0 ? [[DELIVERY_DATES_FIELD, i - 1]] : undefined}
            rules={[
              { required, message: requiredMessage },
              {
                validator: async (_rule, value?: Dayjs | null) => {
                  if (value && previous && value.isBefore(previous, 'day'))
                    throw new Error(DELIVERY_TEXT.dateBeforePrevious);
                },
              },
            ]}
          >
            <AppDatePicker
              disabled={disabled}
              disabledDate={(d) => isExpectedDeliveryDateDisabled(d, previous ?? notBefore)}
            />
          </Form.Item>
        );
      })}
    </Space>
  );
}
