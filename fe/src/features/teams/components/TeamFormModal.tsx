import { useEffect } from 'react';
import { Form, Input, Modal, Select } from 'antd';
import { TEAM_TYPE_LABEL, type TeamType } from '@/constants/hr';
import { MESSAGES } from '@/constants/messages';
import { useSaveTeam } from '../hooks/useTeams';
import type { SaveTeamPayload, TeamListItem } from '../types';

type TeamFormModalProps = { open: boolean; team: TeamListItem | null; onClose: () => void };

const TYPE_OPTIONS = (Object.keys(TEAM_TYPE_LABEL) as TeamType[]).map((t) => ({ value: t, label: TEAM_TYPE_LABEL[t] }));

export function TeamFormModal({ open, team, onClose }: TeamFormModalProps) {
  const [form] = Form.useForm<SaveTeamPayload>();
  const save = useSaveTeam();

  useEffect(() => {
    if (!open) return;
    form.resetFields();
    if (team) form.setFieldsValue({ teamCode: team.teamCode, teamName: team.teamName, teamType: team.teamType });
  }, [open, team, form]);

  const handleFinish = (values: SaveTeamPayload) =>
    save.mutate({ id: team?.teamId, body: values }, {
      onSuccess: onClose,
      onError: (error) =>
        form.setFields(Object.entries(error.errors ?? {}).map(([name, errors]) => ({ name: name as keyof SaveTeamPayload, errors }))),
    });

  return (
    <Modal
      open={open}
      title={team ? `${MESSAGES.common.edit} ${team.teamCode}` : MESSAGES.hr.addTeam}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={save.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Form form={form} layout="vertical" onFinish={handleFinish} disabled={save.isPending}>
        <Form.Item name="teamCode" label={MESSAGES.hr.teamCode} rules={[{ required: true, whitespace: true, message: MESSAGES.common.required }, { max: 20 }]}>
          <Input />
        </Form.Item>
        <Form.Item name="teamName" label={MESSAGES.hr.teamName} rules={[{ required: true, whitespace: true, message: MESSAGES.common.required }, { max: 100 }]}>
          <Input />
        </Form.Item>
        <Form.Item name="teamType" label={MESSAGES.hr.teamType} rules={[{ required: true, message: MESSAGES.common.required }]}>
          <Select options={TYPE_OPTIONS} />
        </Form.Item>
      </Form>
    </Modal>
  );
}
