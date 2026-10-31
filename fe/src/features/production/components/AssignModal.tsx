import { Form, Modal, Select } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { PRODUCTION_TEXT } from '../constants';
import { useAssignableOperations, useAssignOperations } from '../hooks/useProduction';
import { useWorkerOptions } from '../hooks/useWorkerOptions';

const text = PRODUCTION_TEXT.assignments;
const required = [{ required: true, message: PRODUCTION_TEXT.required }];

export function AssignModal({
  open,
  orderId,
  onClose,
}: {
  open: boolean;
  orderId: number;
  onClose: () => void;
}) {
  const [form] = Form.useForm<{ operationIds: number[]; workerId: number }>();
  const operations = useAssignableOperations(orderId);
  const workers = useWorkerOptions();
  const assign = useAssignOperations();

  return (
    <Modal
      open={open}
      title={text.assignTitle}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={assign.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Form
        form={form}
        layout="vertical"
        preserve={false}
        onFinish={(values) => assign.mutate({ orderId, ...values }, { onSuccess: onClose })}
      >
        <Form.Item name="operationIds" label={text.form.operations} rules={required}>
          <Select
            mode="multiple"
            showSearch
            optionFilterProp="label"
            loading={operations.isLoading}
            options={(operations.data ?? []).map((o) => ({
              value: o.operationId,
              label: `${o.productCode} · ${o.stationSequence}. ${o.operationCode} - ${o.operationName}${o.groupName ? ` [${o.groupName}]` : ''}`,
            }))}
          />
        </Form.Item>
        <Form.Item name="workerId" label={text.form.worker} rules={required}>
          <Select
            showSearch
            optionFilterProp="label"
            loading={workers.loading}
            options={workers.options}
          />
        </Form.Item>
      </Form>
    </Modal>
  );
}
