import { Alert } from 'antd';
import { fill } from '@/utils/format';
import { PRODUCTION_TEXT } from '../constants';
import type { OutputGridRow } from '../types';
import { excessOf, remainingOf } from '../utils/outputWorkerBoard';

const t = PRODUCTION_TEXT.output.byWorker;

type WorkerOutputAlertsProps = {
  /** Dòng báo vượt số được giao mà không chuyển được phần vượt: chặn lưu. */
  overRows: OutputGridRow[];
  /** Dòng báo vượt nhưng chuyển được phần vượt từ người cùng công đoạn / phần tổ chưa giao. */
  transferable: { row: OutputGridRow; quantity: number }[];
};

/** Cảnh báo trong khung nhập sản lượng của công nhân: vượt số được giao, và phần vượt sẽ được chuyển khi lưu. */
export function WorkerOutputAlerts({ overRows, transferable }: WorkerOutputAlertsProps) {
  return (
    <>
      {overRows.length > 0 && (
        <Alert
          type="error"
          showIcon
          message={t.overTitle}
          description={overRows.map((r) => (
            <div key={r.operationId}>
              {r.transferableQty > 0
                ? fill(t.overCapTransfer, r.operationCode, remainingOf(r), r.transferableQty)
                : r.capQty > 0
                  ? fill(t.overCap, r.operationCode, remainingOf(r))
                  : fill(t.overNotAllocated, r.operationCode)}
            </div>
          ))}
        />
      )}
      {transferable.length > 0 && (
        <Alert
          type="warning"
          showIcon
          message={t.transferTitle}
          description={transferable.map(({ row, quantity }) => (
            <div key={row.operationId}>
              {fill(t.transferLine, row.operationCode, excessOf(row, quantity))}
            </div>
          ))}
        />
      )}
    </>
  );
}
