import { Form, Modal } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { MESSAGES } from '@/constants/messages';
import { API_DATE } from '@/utils/format';
import { GR_LABELS } from '../constants';
import { useUpdateLevel } from '../hooks/useGrade';
import type { GradeLevel } from '../types';
import { AppDatePicker } from '@/components/common/DateInputs';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { REQUIRED_RULES } from '../constants';
import { MoneyInput } from './MoneyInput';

/** Sửa mức tiền bậc chưa hiệu lực (mức đã hiệu lực là lịch sử lương, không sửa). */
export function EditLevelModal({
  level,
  onClose,
}: {
  level: GradeLevel | null;
  onClose: () => void;
}) {
  const [form] = Form.useForm<{ monthlyAmount: number; effectiveDate: Dayjs }>();
  const update = useUpdateLevel();
  return (
    <Modal
      open={level !== null}
      title={GR_LABELS.editLevel}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      width={MODAL_WIDTH.sm}
      confirmLoading={update.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      {level && (
        <Form
          form={form}
          layout="vertical"
          disabled={update.isPending}
          initialValues={{
            monthlyAmount: level.monthlyAmount,
            effectiveDate: dayjs(level.effectiveDate),
          }}
          onFinish={(v) =>
            update.mutate(
              {
                id: level.gradeLevelId,
                body: {
                  monthlyAmount: v.monthlyAmount,
                  effectiveDate: v.effectiveDate.format(API_DATE),
                },
              },
              { onSuccess: onClose },
            )
          }
        >
          <Form.Item name="monthlyAmount" label={GR_LABELS.newAmount} rules={REQUIRED_RULES}>
            <MoneyInput />
          </Form.Item>
          <Form.Item name="effectiveDate" label={GR_LABELS.effectiveDate} rules={REQUIRED_RULES}>
            <AppDatePicker allowClear={false} disabledDate={(d) => !d.isAfter(dayjs(), 'day')} />
          </Form.Item>
        </Form>
      )}
    </Modal>
  );
}
