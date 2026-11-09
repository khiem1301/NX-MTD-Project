import { REASON_MAX_LENGTH } from '@/constants/limits';
import { Empty, Form, Input, Modal, Select } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { ROLE_LABEL } from '@/constants/hr';
import type { Role } from '@/constants/roles';
import { useAddMembers, useTeamCandidates } from '../hooks/useTeams';

type AddMembersModalProps = {
  open: boolean;
  teamId: number;
  onClose: () => void;
};

type FormValues = { employeeIds: number[]; reason?: string };

/** Thêm nhân viên chưa thuộc tổ nào vào tổ. Người đang ở tổ khác thì dùng "Chuyển sang tổ khác". */
export function AddMembersModal({ open, teamId, onClose }: AddMembersModalProps) {
  const [form] = Form.useForm<FormValues>();
  const candidates = useTeamCandidates(open);
  const add = useAddMembers();

  const options = (candidates.data ?? []).map((c) => ({
    value: c.employeeId,
    label: `${c.employeeCode} · ${c.fullName} (${ROLE_LABEL[c.role as Role] ?? c.role})`,
  }));

  const handleFinish = (v: FormValues) =>
    add.mutate(
      { id: teamId, body: { employeeIds: v.employeeIds, reason: v.reason?.trim() || null } },
      { onSuccess: onClose },
    );

  return (
    <Modal
      open={open}
      title={MESSAGES.hr.addMembers}
      okText={MESSAGES.common.confirm}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={add.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Form form={form} layout="vertical" preserve={false} onFinish={handleFinish}>
        <Form.Item
          name="employeeIds"
          label={MESSAGES.hr.addMembersPick}
          rules={[{ required: true, message: MESSAGES.common.required }]}
        >
          <Select
            mode="multiple"
            showSearch
            optionFilterProp="label"
            loading={candidates.isFetching}
            options={options}
            notFoundContent={<Empty description={MESSAGES.hr.addMembersEmpty} />}
          />
        </Form.Item>
        <Form.Item
          name="reason"
          label={MESSAGES.hr.transferReason}
          rules={[{ max: REASON_MAX_LENGTH }]}
        >
          <Input.TextArea rows={2} />
        </Form.Item>
      </Form>
    </Modal>
  );
}
