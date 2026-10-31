import { Alert, Button, Grid, Input, Space, Typography } from 'antd';
import { QueryError } from '@/components/common/QueryError';
import { formatQty } from '@/utils/format';
import { PRODUCTION_TEXT } from '../constants';
import type { OutputGridRow, OutputRole } from '../types';
import { PageCard } from '@/components/common/PageCard';
import { DataTable } from '@/components/common/DataTable';
import { outputColumns } from './outputColumns';
import { OutputExtraRowBar } from './OutputExtraRowBar';
import { OutputMobileList } from './OutputMobileList';
import { useOutputEntry } from '../hooks/useOutputEntry';
import { OutputFilterBar } from './OutputFilterBar';
import { outputKeyOf } from '../utils';

const text = PRODUCTION_TEXT.output;

/**
 * UC55 (KI-04) / UC22 (KI-11) — lưới sản lượng cuối ca: mỗi dòng là một công nhân × công đoạn đã phân công.
 * Tổ trưởng / tổ phó nhập và sửa trong hạn; Quản đốc sửa hộ tổ kể cả sau hạn, bắt buộc ghi lý do.
 * Số lượng 0 ở dòng đã nhập nghĩa là xóa dòng đó.
 */
export function OutputEntryBoard({ role }: { role: OutputRole }) {
  const isManager = role === 'manager';
  // Trên điện thoại (KI-14) lưới hiện dạng thẻ: mỗi công nhân một thẻ, ô nhập số lớn dễ bấm.
  const isMobile = Grid.useBreakpoint().md === false;
  const entry = useOutputEntry(role);
  const {
    edits,
    reason,
    setReason,
    warnings,
    setWarnings,
    extraRows,
    extraOperationId,
    setExtraOperationId,
    extraWorkerId,
    setExtraWorkerId,
    operations,
    members,
    save,
    query,
    grid,
    rows,
    changedRows,
    addExtraRow,
    removeExtraRow,
    handleSave,
    changeQuantity,
  } = entry;

  const columns = outputColumns({
    isManager,
    edits,
    isExtraRow: (r) => extraRows.some((e) => outputKeyOf(e) === outputKeyOf(r)),
    onQuantityChange: changeQuantity,
    onRemoveExtra: removeExtraRow,
  });

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

      {!isManager && query !== null && (
        <OutputExtraRowBar
          operationOptions={(operations.data ?? []).map((o) => ({
            value: o.operationId,
            label: `${o.operationCode} - ${o.operationName}`,
          }))}
          workerOptions={(members.data ?? [])
            .filter((m) => m.role === 'Worker')
            .map((m) => ({ value: m.employeeId, label: `${m.employeeCode} - ${m.fullName}` }))}
          operationsLoading={operations.isLoading}
          workersLoading={members.isLoading}
          operationId={extraOperationId}
          workerId={extraWorkerId}
          onOperationChange={setExtraOperationId}
          onWorkerChange={setExtraWorkerId}
          onAdd={addExtraRow}
        />
      )}

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
          {isManager && (
            <Input.TextArea
              rows={2}
              maxLength={500}
              showCount
              placeholder={text.reason}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
            />
          )}
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
    </PageCard>
  );
}
