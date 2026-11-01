import { Alert, Form, Input, Modal, Select } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { MESSAGES } from '@/constants/messages';
import { API_DATE } from '@/utils/format';
import { GR_LABELS } from '../constants';
import { useAllGradeWorkers, useGradeOptions, useSetGrade } from '../hooks/useGrade';
import { AppDatePicker } from '@/components/common/DateInputs';
import { REQUIRED_RULES } from '../constants';

export function SetGradeModal({ open, onClose }: { open: boolean; onClose: () => void }) {
  const gradeOptions = useGradeOptions();
  const [form] = Form.useForm<{
    teamKey: string;
    workerId: number;
    gradeCode: string;
    reason: string;
    effectiveDate: Dayjs;
  }>();
  const workers = useAllGradeWorkers(open);
  const teamKey = Form.useWatch('teamKey', form);
  const workerId = Form.useWatch('workerId', form);
  const all = workers.data ?? [];
  const teamOptions = [
    ...new Map(
      all.map((w) => [String(w.teamId ?? 'none'), w.teamName ?? GR_LABELS.noTeam]),
    ).entries(),
  ].map(([value, label]) => ({ value, label }));
  const inTeam = all.filter((w) => String(w.teamId ?? 'none') === teamKey);
  const selected = all.find((w) => w.workerId === workerId);
  const setGrade = useSetGrade();
  return (
    <Modal
      open={open}
      title={GR_LABELS.setDirect}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={setGrade.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Alert type="info" showIcon style={{ marginBottom: 12 }} message={GR_LABELS.setDirectHint} />
      <Form
        form={form}
        layout="vertical"
        disabled={setGrade.isPending}
        initialValues={{ effectiveDate: dayjs() }}
        onFinish={(v) =>
          setGrade.mutate(
            {
              workerId: v.workerId,
              gradeCode: v.gradeCode,
              reason: v.reason.trim(),
              effectiveDate: v.effectiveDate.format(API_DATE),
            },
            { onSuccess: onClose },
          )
        }
      >
        <Form.Item name="teamKey" label={GR_LABELS.team} rules={REQUIRED_RULES}>
          <Select
            showSearch
            optionFilterProp="label"
            placeholder={GR_LABELS.selectTeam}
            loading={workers.isLoading}
            options={teamOptions}
            onChange={() => form.setFieldValue('workerId', undefined)}
          />
        </Form.Item>
        <Form.Item name="workerId" label={GR_LABELS.worker} rules={REQUIRED_RULES}>
          <Select
            showSearch
            optionFilterProp="label"
            disabled={!teamKey}
            placeholder={teamKey ? GR_LABELS.selectWorker : GR_LABELS.selectTeamFirst}
            options={inTeam.map((w) => ({
              value: w.workerId,
              label: `${w.workerCode} - ${w.fullName} · ${GR_LABELS.currentGrade}: ${w.currentGradeCode ?? GR_LABELS.none}${w.hasPending ? ` (${GR_LABELS.pending})` : ''}`,
            }))}
          />
        </Form.Item>
        {selected && (
          <Form.Item label={GR_LABELS.currentGrade}>
            <Input
              disabled
              value={selected.currentGradeCode ?? GR_LABELS.none}
              style={{ width: 160 }}
            />
          </Form.Item>
        )}
        <Form.Item name="gradeCode" label={GR_LABELS.newGrade} rules={REQUIRED_RULES}>
          <Select
            options={gradeOptions.options}
            loading={gradeOptions.isLoading}
            style={{ width: 200 }}
          />
        </Form.Item>
        <Form.Item name="effectiveDate" label={GR_LABELS.effectiveDate} rules={REQUIRED_RULES}>
          <AppDatePicker />
        </Form.Item>
        <Form.Item
          name="reason"
          label={GR_LABELS.setReason}
          rules={[{ required: true, whitespace: true, message: MESSAGES.common.required }]}
        >
          <Input.TextArea rows={3} maxLength={500} showCount />
        </Form.Item>
      </Form>
    </Modal>
  );
}
