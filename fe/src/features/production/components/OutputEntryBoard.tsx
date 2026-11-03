import { Alert, Button, Grid, Input, Space, Typography } from 'antd';
import { QueryError } from '@/components/common/QueryError';
import { formatQty } from '@/utils/format';
import { PRODUCTION_TEXT } from '../constants';
import type { OutputGridRow, OutputRole } from '../types';
import { PageCard } from '@/components/common/PageCard';
import { DataTable } from '@/components/common/DataTable';
import { outputColumns } from './outputColumns';
import { OutputByWorker } from './OutputByWorker';
import { OutputMobileList } from './OutputMobileList';
import { useOutputEntry } from '../hooks/useOutputEntry';
import { OutputFilterBar } from './OutputFilterBar';
import { outputKeyOf } from '../utils';

const text = PRODUCTION_TEXT.output;

/**
 * UC55 (KI-04) / UC22 (KI-11) — sản lượng cuối ca. Tổ trưởng / tổ phó nhập theo từng công nhân (OutputByWorker).
 * Quản đốc sửa hộ tổ trên lưới công nhân × công đoạn, kể cả sau hạn, bắt buộc ghi lý do. Số lượng 0 ở dòng đã nhập nghĩa là xóa dòng đó.
 */
export function OutputEntryBoard({ role }: { role: OutputRole }) {
  const isManager = role === 'manager';
  // Trên điện thoại (KI-14) lưới của Quản đốc hiện dạng thẻ: mỗi công nhân một thẻ, ô nhập số lớn dễ bấm.
  const isMobile = Grid.useBreakpoint().md === false;
  const entry = useOutputEntry(role);
  const {
    edits,
    reason,
    setReason,
    warnings,
    setWarnings,
    save,
    query,
    grid,
    rows,
    changedRows,
    handleSave,
    changeQuantity,
  } = entry;

  const columns = outputColumns({ isManager, edits, onQuantityChange: changeQuantity });

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

      {!isManager ? (
        <OutputByWorker entry={entry} />
      ) : (
        <>
          {isMobile ? (
            <OutputMobileList
              rows={rows}
              isManager={isManager}
              loading={grid.isFetching}
              edits={edits}
              noQuery={query === null}
              onQuantityChange={changeQuantity}
            />
          ) : (
            <DataTable<OutputGridRow>
              rowKey={outputKeyOf}
              size="small"
              columns={columns}
              dataSource={rows}
              loading={grid.isFetching}
              locale={{ emptyText: query === null ? PRODUCTION_TEXT.selectOrderLine : text.noRows }}
              scroll={{ x: 'max-content' }}
              pagination={false}
            />
          )}

          {rows.length > 0 && (
            <Space direction="vertical" style={{ width: '100%' }}>
              <Input.TextArea
                rows={2}
                maxLength={500}
                showCount
                placeholder={text.reason}
                value={reason}
                onChange={(e) => setReason(e.target.value)}
              />
              <Button
                type="primary"
                loading={save.isPending}
                disabled={changedRows.length === 0}
                onClick={handleSave}
              >
                {`${text.save}${changedRows.length > 0 ? ` (${changedRows.length})` : ''}`}
              </Button>
            </Space>
          )}
        </>
      )}
    </PageCard>
  );
}
