import { DatePicker, Form, Input, Modal, Select } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { MESSAGES } from '@/constants/messages';
import { useTeamLookup } from '@/hooks/useHrLookups';
import { toApiDate } from '@/utils/format';
import { useTransferIn } from '../hooks/useTeams';

type TransferModalProps = {
  open: boolean;
  /** Tổ hiện tại: bị loại khỏi danh sách tổ đích. */
  fromTeamId: number;
  employeeIds: number[];
  onClose: () => void;
};

type FormValues = { targetTeamId: number; effectiveDate: Dayjs; reason?: string };

/** D32: ngày hiệu lực không được sau hôm nay; TeamId đổi ngay khi lưu, ngày ghi vào lịch sử. */
export function TransferModal({ open, fromTeamId, employeeIds, onClose }: TransferModalProps) {
  const [form] = Form.useForm<FormValues>();
  const teams = useTeamLookup();
  const transfer = useTransferIn();

  const handleFinish = (v: FormValues) =>
    transfer.mutate(
      { id: v.targetTeamId, body: { employeeIds, effectiveDate: toApiDate(v.effectiveDate), reason: v.reason?.trim() || null } },
      { onSuccess: onClose },
    );

  return (
    <Modal
      open={open}
      title={`${MESSAGES.hr.transfer} (${employeeIds.length})`}
      okText={MESSAGES.common.confirm}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={transfer.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Form form={form} layout="vertical" preserve={false} initialValues={{ effectiveDate: dayjs() }} onFinish={handleFinish}>
        <Form.Item name="targetTeamId" label={MESSAGES.hr.transferTarget} rules={[{ required: true, message: MESSAGES.common.required }]}>
          <Select
            showSearch
            optionFilterProp="label"
            options={(teams.data ?? []).filter((t) => t.teamId !== fromTeamId).map((t) => ({ value: t.teamId, label: t.teamName }))}
          />
        </Form.Item>
        <Form.Item name="effectiveDate" label={MESSAGES.hr.effectiveDate} rules={[{ required: true, message: MESSAGES.common.required }]}>
          <DatePicker format="DD/MM/YYYY" disabledDate={(d) => d.isAfter(dayjs(), 'day')} />
        </Form.Item>
        <Form.Item name="reason" label={MESSAGES.hr.transferReason} rules={[{ max: 500 }]}>
          <Input.TextArea rows={2} />
        </Form.Item>
      </Form>
    </Modal>
  );
}
