import { Form, Input, Modal, Radio, Select } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { useAnnouncementTargets, useSendAnnouncement } from '../hooks/useNotifications';
import { PRIORITY_OPTIONS, SCOPE_OPTIONS } from '../constants';

type SendForm = {
  content: string;
  priority: string;
  targetScope: string;
  teamIds?: number[];
  roleIds?: number[];
};

export function SendModal({ open, onClose }: { open: boolean; onClose: () => void }) {
  const [form] = Form.useForm<SendForm>();
  const targets = useAnnouncementTargets(open);
  const send = useSendAnnouncement();
  const scope = Form.useWatch('targetScope', form) ?? 'ALL';

  return (
    <Modal
      open={open}
      title="Gửi thông báo nhanh"
      okText="Gửi"
      cancelText={MESSAGES.common.cancel}
      confirmLoading={send.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Form
        form={form}
        layout="vertical"
        disabled={send.isPending}
        initialValues={{ priority: 'NORMAL', targetScope: 'ALL' }}
        onFinish={(v) => send.mutate({ ...v, content: v.content.trim() }, { onSuccess: onClose })}
      >
        <Form.Item
          name="content"
          label="Nội dung"
          rules={[{ required: true, whitespace: true, message: MESSAGES.common.required }]}
        >
          <Input.TextArea rows={4} maxLength={1000} showCount />
        </Form.Item>
        <Form.Item name="priority" label="Mức ưu tiên">
          <Radio.Group optionType="button" options={PRIORITY_OPTIONS} />
        </Form.Item>
        <Form.Item name="targetScope" label="Gửi tới">
          <Radio.Group optionType="button" options={SCOPE_OPTIONS} />
        </Form.Item>
        {scope === 'TEAM' && (
          <Form.Item
            name="teamIds"
            label="Tổ"
            rules={[{ required: true, message: MESSAGES.common.required }]}
          >
            <Select
              mode="multiple"
              loading={targets.isLoading}
              options={(targets.data?.teams ?? []).map((t) => ({
                value: t.teamId,
                label: t.teamName,
              }))}
            />
          </Form.Item>
        )}
        {scope === 'ROLE' && (
          <Form.Item
            name="roleIds"
            label="Vai trò"
            rules={[{ required: true, message: MESSAGES.common.required }]}
          >
            <Select
              mode="multiple"
              loading={targets.isLoading}
              options={(targets.data?.roles ?? []).map((r) => ({
                value: r.roleId,
                label: r.roleName,
              }))}
            />
          </Form.Item>
        )}
      </Form>
    </Modal>
  );
}
