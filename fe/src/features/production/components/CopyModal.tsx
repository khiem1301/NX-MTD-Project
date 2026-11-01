import { Form, Modal, Typography } from 'antd';
import { OrderSelect } from '@/components/common/OrderSelect';
import { MESSAGES } from '@/constants/messages';
import { PRODUCTION_TEXT } from '../constants';
import { useCopyAssignments } from '../hooks/useProduction';

const text = PRODUCTION_TEXT.assignments;
const required = [{ required: true, message: PRODUCTION_TEXT.required }];

export function CopyModal({
  open,
  toOrderId,
  onClose,
}: {
  open: boolean;
  toOrderId: number;
  onClose: () => void;
}) {
  const [form] = Form.useForm<{ fromOrderId: number }>();
  const copy = useCopyAssignments();

  return (
    <Modal
      open={open}
      title={text.copyTitle}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={copy.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Typography.Paragraph type="secondary">{text.copyHint}</Typography.Paragraph>
      <Form
        form={form}
        layout="vertical"
        preserve={false}
        onFinish={(values) =>
          copy.mutate({ fromOrderId: values.fromOrderId, toOrderId }, { onSuccess: onClose })
        }
      >
        <Form.Item name="fromOrderId" label={text.copyFrom} rules={required}>
          <OrderSelect width="100%" />
        </Form.Item>
      </Form>
    </Modal>
  );
}
