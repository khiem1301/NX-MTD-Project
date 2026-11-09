import { Alert, Typography } from 'antd';
import { QueryError } from '@/components/common/QueryError';
import { formatQty } from '@/utils/format';
import { PRODUCTION_TEXT } from '../constants';
import type { OutputRole } from '../types';
import { PageCard } from '@/components/common/PageCard';
import { OutputByWorker } from './OutputByWorker';
import { useOutputEntry } from '../hooks/useOutputEntry';
import { OutputFilterBar } from './OutputFilterBar';

const text = PRODUCTION_TEXT.output;

/**
 * UC55 (KI-04) / UC22 (KI-11) — sản lượng cuối ca. Tổ trưởng / tổ phó và Quản đốc cùng nhập theo từng công nhân
 * (OutputByWorker). Quản đốc chọn thêm tổ may, sửa được cả sau hạn, bắt buộc ghi lý do trong khung nhập của công nhân.
 * Số lượng 0 ở dòng đã nhập nghĩa là xóa dòng đó.
 */
export function OutputEntryBoard({ role }: { role: OutputRole }) {
  const entry = useOutputEntry(role);
  const { isManager, warnings, setWarnings, grid } = entry;

  return (
    <PageCard
      title={isManager ? text.managerTitle : text.title}
      description={isManager ? text.managerSubtitle : text.subtitle}
    >
      <OutputFilterBar entry={entry} isManager={isManager} />

      <QueryError error={grid.error} onRetry={() => void grid.refetch()} />

      {grid.data && (
        <Typography.Text>
          {text.received}: <strong>{formatQty(grid.data.receivedFromWarehouse)}</strong>
        </Typography.Text>
      )}

      {warnings.length > 0 && (
        <Alert
          type="warning"
          showIcon
          message={warnings.map((w) => (
            <div key={w}>{w}</div>
          ))}
          closable
          onClose={() => setWarnings([])}
        />
      )}

      <OutputByWorker entry={entry} />
    </PageCard>
  );
}
