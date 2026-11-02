import { Form, Modal, Typography, message } from 'antd';
import { OrderSelect } from '@/components/common/OrderSelect';
import { MESSAGES } from '@/constants/messages';
import { fill } from '@/utils/format';
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
          copy.mutate(
            { fromOrderId: values.fromOrderId, toOrderId },
            {
              onSuccess: (count) => {
                if (count === 0) void message.info(text.done.noneCopied);
                else void message.success(fill(text.done.copied, count));
                onClose();
              },
            },
          )
        }
      >
        <Form.Item
          name="fromOrderId"
          label={text.copyFrom}
          rules={[
            ...required,
            {
              validator: (_, value) =>
                value === toOrderId
                  ? Promise.reject(new Error(text.copySameOrder))
                  : Promise.resolve(),
            },
          ]}
        >
          <OrderSelect width="100%" />
        </Form.Item>
      </Form>
    </Modal>
  );
}
