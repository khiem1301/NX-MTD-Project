import { InputNumber, type InputNumberProps } from 'antd';
import { moneyFormatter, moneyParser } from '../utils';

/** Ô nhập tiền: phải chuyển tiếp props vì Form.Item truyền value / onChange vào phần tử con trực tiếp. */
export const MoneyInput = (props: InputNumberProps) => (
  <InputNumber
    min={0}
    step={50000}
    suffix="đ"
    formatter={moneyFormatter}
    parser={moneyParser}
    style={{ width: '100%' }}
    {...props}
  />
);
