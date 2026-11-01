import { Form, Input, Modal } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { AppDatePicker } from '@/components/common/DateInputs';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { MESSAGES } from '@/constants/messages';
import { API_DATE } from '@/utils/format';
import { GR_LABELS, REQUIRED_RULES } from '../constants';
import { useAddLevel, useCreateGradeCode, useUpdateGradeCode } from '../hooks/useGrade';
import type { GradeCode } from '../types';
import { MoneyInput } from './MoneyInput';

type CodeForm = {
  gradeCode: string;
  gradeName: string;
  monthlyAmount?: number;
  effectiveDate?: Dayjs;
};

type GradeCodeFormModalProps = {
  /** 'new' = thêm bậc; một bậc = sửa tên; null = đóng. */
  editing: GradeCode | 'new' | null;
  onClose: () => void;
};

/** Thêm bậc (kèm mức tiền đầu tiên) hoặc sửa tên bậc. */
export function GradeCodeFormModal({ editing, onClose }: GradeCodeFormModalProps) {
  const [form] = Form.useForm<CodeForm>();
  const create = useCreateGradeCode();
  const addLevel = useAddLevel();
  const update = useUpdateGradeCode();
  const isNew = editing === 'new';
  const current = editing !== null && editing !== 'new' ? editing : null;

  const submit = (v: CodeForm) => {
    const done = { onSuccess: onClose };
    if (current) {
      update.mutate(
        {
          code: current.gradeCode,
          body: { gradeName: v.gradeName.trim(), status: current.status },
        },
        done,
      );
      return;
    }
    const gradeCode = v.gradeCode.trim().toUpperCase();
    const gradeName = v.gradeName.trim();
    // Tạo bậc rồi đặt luôn mức tiền đầu tiên; nếu bước tiền lỗi thì bậc vẫn còn, dùng "Đổi tiền" để đặt lại.
    create.mutate(
      { gradeCode, gradeName },
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
      open={editing !== null}
      title={isNew ? GR_LABELS.addCode : GR_LABELS.editCode}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      width={MODAL_WIDTH.sm}
      confirmLoading={create.isPending || addLevel.isPending || update.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      {editing !== null && (
        <Form
          form={form}
          layout="vertical"
          onFinish={submit}
          initialValues={
            current
              ? { gradeCode: current.gradeCode, gradeName: current.gradeName }
              : { effectiveDate: dayjs() }
          }
        >
          <Form.Item
            name="gradeCode"
            label={GR_LABELS.codeCode}
            rules={[
              { required: true, message: MESSAGES.common.required },
              { pattern: /^[A-Za-z0-9]{1,5}$/, message: 'Chỉ gồm chữ và số, tối đa 5 ký tự.' },
            ]}
          >
            <Input maxLength={5} disabled={!isNew} style={{ width: 160 }} />
          </Form.Item>
          <Form.Item
            name="gradeName"
            label={GR_LABELS.gradeName}
            rules={[{ required: true, whitespace: true, message: MESSAGES.common.required }]}
          >
            <Input maxLength={50} />
          </Form.Item>
          {isNew && (
            <>
              <Form.Item name="monthlyAmount" label={GR_LABELS.amount} rules={REQUIRED_RULES}>
                <MoneyInput />
              </Form.Item>
              <Form.Item
                name="effectiveDate"
                label={GR_LABELS.effectiveDate}
                rules={REQUIRED_RULES}
              >
                <AppDatePicker allowClear={false} />
              </Form.Item>
            </>
          )}
        </Form>
      )}
    </Modal>
  );
}
