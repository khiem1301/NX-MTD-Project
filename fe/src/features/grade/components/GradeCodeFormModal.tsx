import { Form, Input, Modal } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { AppDatePicker } from '@/components/common/DateInputs';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { MESSAGES } from '@/constants/messages';
import { API_DATE } from '@/utils/format';
import { GR_LABELS, REQUIRED_RULES } from '../constants';
import { useAddLevel, useCreateGradeCode } from '../hooks/useGrade';
import { MoneyInput } from './MoneyInput';

type CodeForm = {
  gradeCode: string;
  monthlyAmount?: number;
  effectiveDate?: Dayjs;
};

type GradeCodeFormModalProps = {
  open: boolean;
  onClose: () => void;
};

/** Thêm bậc kèm mức tiền đầu tiên. Bậc chỉ có mã (không có tên) nên không có form sửa. */
export function GradeCodeFormModal({ open, onClose }: GradeCodeFormModalProps) {
  const [form] = Form.useForm<CodeForm>();
  const create = useCreateGradeCode();
  const addLevel = useAddLevel();

  const submit = (v: CodeForm) => {
    const gradeCode = v.gradeCode.trim().toUpperCase();
    // Tạo bậc rồi đặt luôn mức tiền đầu tiên; nếu bước tiền lỗi thì bậc vẫn còn, dùng "Đổi tiền" để đặt lại.
    create.mutate(
      { gradeCode },
      {
        onSuccess: () =>
          addLevel.mutate(
            {
              gradeCode,
              monthlyAmount: v.monthlyAmount ?? 0,
              effectiveDate: (v.effectiveDate ?? dayjs()).format(API_DATE),
            },
            { onSettled: onClose },
          ),
      },
    );
  };

  return (
    <Modal
      open={open}
      title={GR_LABELS.addCode}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      width={MODAL_WIDTH.sm}
      confirmLoading={create.isPending || addLevel.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      {open && (
        <Form
          form={form}
          layout="vertical"
          onFinish={submit}
          initialValues={{ effectiveDate: dayjs() }}
        >
          <Form.Item
            name="gradeCode"
            label={GR_LABELS.codeCode}
            normalize={(v: string | undefined) => v?.replace(/[^A-Za-z0-9]/g, '')}
            rules={[
              { required: true, message: MESSAGES.common.required },
              { pattern: /^[A-Za-z0-9]{1,5}$/, message: 'Chỉ gồm chữ và số, tối đa 5 ký tự.' },
            ]}
          >
            <Input maxLength={5} style={{ width: 160 }} />
          </Form.Item>
          <Form.Item name="monthlyAmount" label={GR_LABELS.amount} rules={REQUIRED_RULES}>
            <MoneyInput />
          </Form.Item>
          <Form.Item name="effectiveDate" label={GR_LABELS.effectiveDate} rules={REQUIRED_RULES}>
            <AppDatePicker allowClear={false} />
          </Form.Item>
        </Form>
      )}
    </Modal>
  );
}
