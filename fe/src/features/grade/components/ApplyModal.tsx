import { Alert, Form, Input, Modal } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { MESSAGES } from '@/constants/messages';
import { API_DATE } from '@/utils/format';
import { GR_LABELS } from '../constants';
import { useApplyGrade } from '../hooks/useGrade';
import type { GradeEvaluation } from '../types';
import { AppDatePicker } from '@/components/common/DateInputs';
import { REQUIRED_RULES } from '../constants';

export function ApplyModal({
  evaluation,
  onClose,
}: {
  evaluation: GradeEvaluation | null;
  onClose: () => void;
}) {
  const [form] = Form.useForm<{ effectiveDate: Dayjs; decisionNote?: string }>();
  const apply = useApplyGrade();
  return (
    <Modal
      open={evaluation !== null}
      title={GR_LABELS.applyTitle}
      okText={MESSAGES.common.confirm}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={apply.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      {evaluation && (
        <Form
          form={form}
          layout="vertical"
          initialValues={{ effectiveDate: dayjs() }}
          onFinish={(v) =>
            apply.mutate(
              {
                id: evaluation.gradeEvaluationId,
                body: {
                  effectiveDate: v.effectiveDate.format(API_DATE),
                  decisionNote: v.decisionNote?.trim() || undefined,
                },
              },
              { onSuccess: onClose },
            )
          }
        >
          <Alert
            type="info"
            showIcon
            style={{ marginBottom: 12 }}
            message={`${evaluation.workerName}: ${evaluation.currentGradeCode ?? GR_LABELS.none} → ${evaluation.proposedGradeCode}`}
          />
          <Form.Item name="effectiveDate" label={GR_LABELS.effectiveDate} rules={REQUIRED_RULES}>
            <AppDatePicker />
          </Form.Item>
          <Form.Item name="decisionNote" label={GR_LABELS.decisionNote}>
            <Input.TextArea rows={2} maxLength={500} />
          </Form.Item>
        </Form>
      )}
    </Modal>
  );
}
