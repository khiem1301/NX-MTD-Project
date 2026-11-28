import { fireEvent, render, screen } from '@testing-library/react';
import { Form, Input, InputNumber } from 'antd';
import { revalidate } from './formValidate';

const NEED_REASON = 'Nhập lý do lệch';
const HANDED = 10;

function Sample({ withRevalidate }: { withRevalidate: boolean }) {
  const [form] = Form.useForm();
  return (
    <Form form={form} initialValues={{ qty: HANDED }}>
      <Form.Item name="qty" label="Số nhận">
        <InputNumber onChange={() => withRevalidate && revalidate(form, 'reason')} />
      </Form.Item>
      <Form.Item
        name="reason"
        label="Lý do"
        dependencies={['qty']}
        rules={[
          {
            validator: async (_rule, value?: string) => {
              if (form.getFieldValue('qty') !== HANDED && !value?.trim())
                throw new Error(NEED_REASON);
            },
          },
        ]}
      >
        <Input />
      </Form.Item>
    </Form>
  );
}

describe('revalidate', () => {
  it('đổi số nhận khác số gửi thì lỗi ô lý do hiện ngay dưới ô, không đợi bấm Lưu', async () => {
    render(<Sample withRevalidate />);

    fireEvent.change(screen.getByLabelText('Số nhận'), { target: { value: '7' } });

    expect(await screen.findByText(NEED_REASON)).toBeInTheDocument();
  });

  it('không kiểm lại thì ô lý do chưa từng chạm vào sẽ không báo lỗi (hành vi mặc định của antd)', async () => {
    render(<Sample withRevalidate={false} />);

    fireEvent.change(screen.getByLabelText('Số nhận'), { target: { value: '7' } });
    await screen.findByDisplayValue('7');

    expect(screen.queryByText(NEED_REASON)).not.toBeInTheDocument();
  });
});
