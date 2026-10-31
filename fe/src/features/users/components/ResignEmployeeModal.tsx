import { Form, Input, Modal } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { AppDatePicker } from '@/components/common/DateInputs';
import { MESSAGES } from '@/constants/messages';
import { toApiDate } from '@/utils/format';
import { useResignEmployee } from '../hooks/useEmployees';

type ResignForm = { resignDate: Dayjs; reason: string };

type ResignEmployeeModalProps = {
  employeeId: number;
  rowVer: string;
  open: boolean;
  onClose: () => void;
};

/** Hộp thoại Cho nghỉ việc: chọn ngày nghỉ và nhập lý do. */
export function ResignEmployeeModal({
  employeeId,
  rowVer,
  open,
  onClose,
}: ResignEmployeeModalProps) {
  const resign = useResignEmployee(employeeId);
  const [resignForm] = Form.useForm<ResignForm>();
  return (
    <Modal
      open={open}
      title={MESSAGES.hr.resign}
      okText={MESSAGES.common.confirm}
      cancelText={MESSAGES.common.cancel}
      okButtonProps={{ danger: true }}
      confirmLoading={resign.isPending}
      onCancel={onClose}
      onOk={() => resignForm.submit()}
      destroyOnHidden
    >
      <Form
        form={resignForm}
        layout="vertical"
        preserve={false}
        initialValues={{ resignDate: dayjs() }}
        onFinish={(v) =>
          resign.mutate(
            {
              resignDate: toApiDate(v.resignDate),
              reason: v.reason.trim(),
              rowVer,
            },
            { onSuccess: onClose },
          )
        }
      >
        <Form.Item
          name="resignDate"
          label={MESSAGES.hr.resignDate}
          rules={[{ required: true, message: MESSAGES.common.required }]}
        >
          <AppDatePicker />
        </Form.Item>
        <Form.Item
          name="reason"
          label={MESSAGES.hr.resignReason}
          rules={[{ required: true, whitespace: true, message: MESSAGES.common.reasonRequired }]}
        >
          <Input.TextArea rows={3} maxLength={500} showCount />
        </Form.Item>
      </Form>
    </Modal>
  );
}
