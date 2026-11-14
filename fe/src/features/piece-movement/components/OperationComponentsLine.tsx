import { Typography } from 'antd';
import { PM_LABELS } from '../constants';
import type { DailyAllocationRow } from '../types';
import { componentsText } from '../utils/dailyBoard';

type OperationComponentsLineProps = { row: Pick<DailyAllocationRow, 'components'> };

/** Dòng phụ dưới tên công đoạn: các thành phần (chi tiết vải) của sản phẩm mà công đoạn làm trên đó. */
export function OperationComponentsLine({ row }: OperationComponentsLineProps) {
  const text = componentsText(row);
  return (
    <Typography.Text
      type="secondary"
      style={{ display: 'block', fontSize: 12 }}
      ellipsis={{ tooltip: text }}
    >
      {`${PM_LABELS.components}: ${text || PM_LABELS.noComponents}`}
    </Typography.Text>
  );
}
