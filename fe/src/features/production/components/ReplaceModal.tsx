import { Form, Modal, Select } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { PRODUCTION_TEXT } from '../constants';
import { useReplaceAssignment } from '../hooks/useProduction';
import { useWorkerOptions } from '../hooks/useWorkerOptions';
import type { OperationAssignment } from '../types';

const text = PRODUCTION_TEXT.assignments;
const required = [{ required: true, message: PRODUCTION_TEXT.required }];

export function ReplaceModal({
  target,
  onClose,
}: {
  target: OperationAssignment | null;
  onClose: () => void;
}) {
  const [form] = Form.useForm<{ newWorkerId: number }>();
  const workers = useWorkerOptions();
  const replace = useReplaceAssignment();

  return (
    <Modal
      open={target !== null}
      title={`${text.replaceTitle}: ${target?.operationCode ?? ''}`}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={replace.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Form
        form={form}
        layout="vertical"
        preserve={false}
        onFinish={(values) =>
          target &&
          replace.mutate(
            { id: target.operationAssignmentId, newWorkerId: values.newWorkerId },
            { onSuccess: onClose },
          )
        }
      >
        <Form.Item name="newWorkerId" label={text.form.worker} rules={required}>
          <Select
            showSearch
            optionFilterProp="label"
            loading={workers.loading}
            options={workers.options.filter((o) => o.value !== target?.workerId)}
          />
        </Form.Item>
      </Form>
    </Modal>
  );
}
