import { render, screen } from '@testing-library/react';
import { Form } from 'antd';
import { DeliveryDateFields } from './DeliveryDateFields';

const PICKER = '.ant-picker';

const setup = (count: number) =>
  render(
    <Form>
      <DeliveryDateFields count={count} label="Ngày dự kiến giao" requiredMessage="Chọn ngày." />
    </Form>,
  ).container;

describe('DeliveryDateFields', () => {
  it('giao một lần thì một ô ngày với nhãn gốc', () => {
    const container = setup(1);

    expect(screen.getByText('Ngày dự kiến giao')).toBeInTheDocument();
    expect(container.querySelectorAll(PICKER)).toHaveLength(1);
  });

  it('giao N lần thì đúng N ô ngày, nhãn theo lần giao', () => {
    const container = setup(3);

    expect(container.querySelectorAll(PICKER)).toHaveLength(3);
    expect(screen.getByText('Ngày giao lần 1')).toBeInTheDocument();
    expect(screen.getByText('Ngày giao lần 3')).toBeInTheDocument();
  });

  it('chưa nhập số lần thì không hiện ô nào', () => {
    const container = setup(0);

    expect(container.querySelectorAll(PICKER)).toHaveLength(0);
  });
});
