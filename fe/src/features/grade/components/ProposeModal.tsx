import { Form, Input, Modal, Select } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { GR_LABELS } from '../constants';
import { useGradeOptions, useGradeWorkers, useProposeGrade } from '../hooks/useGrade';
import { REQUIRED_RULES } from '../constants';

export function ProposeModal({ open, onClose }: { open: boolean; onClose: () => void }) {
  const gradeOptions = useGradeOptions();
  const [form] = Form.useForm<{ workerId: number; proposedGradeCode: string; reason: string }>();
  const workers = useGradeWorkers(open);
  const propose = useProposeGrade();
  return (
    <Modal
      open={open}
      title={GR_LABELS.propose}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={propose.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Form
        form={form}
        layout="vertical"
        disabled={propose.isPending}
        onFinish={(v) => propose.mutate(v, { onSuccess: onClose })}
      >
        <Form.Item name="workerId" label={GR_LABELS.worker} rules={REQUIRED_RULES}>
          <Select
            showSearch
            optionFilterProp="label"
            placeholder={GR_LABELS.selectWorker}
            loading={workers.isLoading}
            options={(workers.data ?? []).map((w) => ({
              value: w.workerId,
              disabled: w.hasPending,
              label: `${w.workerCode} - ${w.fullName} · ${GR_LABELS.currentGrade}: ${w.currentGradeCode ?? GR_LABELS.none} · ${GR_LABELS.defects}: ${w.recentDefectCount}${w.hasPending ? ` (${GR_LABELS.pending})` : ''}`,
            }))}
          />
        </Form.Item>
        <Form.Item name="proposedGradeCode" label={GR_LABELS.proposedGrade} rules={REQUIRED_RULES}>
          <Select
            options={gradeOptions.options}
            loading={gradeOptions.isLoading}
            style={{ width: 200 }}
          />
        </Form.Item>
        <Form.Item
          name="reason"
          label={GR_LABELS.reason}
          rules={[{ required: true, whitespace: true, message: MESSAGES.common.required }]}
        >
          <Input.TextArea rows={3} maxLength={500} showCount />
        </Form.Item>
      </Form>
    </Modal>
  );
}
