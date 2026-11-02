import { useState } from 'react';
import { Alert, Empty, Typography } from 'antd';
import { QueryError } from '@/components/common/QueryError';
import { useProductLookup } from '@/hooks/useOrderLookups';
import { fill } from '@/utils/format';
import { OperationFilterBar } from '../components/OperationFilterBar';
import { OperationFormModal } from '../components/OperationFormModal';
import { OperationGroupModal } from '../components/OperationGroupModal';
import { OperationImportModal } from '../components/OperationImportModal';
import { OperationPageActions } from '../components/OperationPageActions';
import { OperationTable } from '../components/OperationTable';
import { PieceRateFormModal } from '../components/PieceRateFormModal';
import { operationColumns } from '../components/operationColumns';
import { PieceRateHistoryModal } from '../components/PieceRateHistory';
import { PRODUCTION_TEXT } from '../constants';
import { useOperationBoard } from '../hooks/useOperationBoard';
import { useOperationRole } from '../hooks/useOperationRole';
import {
  useOperationGroups,
  useOperations,
  useSubmitPieceRate,
  useToggleOperation,
} from '../hooks/useProduction';
import type { Operation } from '../types';
import {
  countOperations,
  draftRateIdsOf,
  filterOperations,
  nextOperationDefaults,
} from '../utils/operationBoard';
import { PageCard } from '@/components/common/PageCard';

const text = PRODUCTION_TEXT.operations;

/**
 * UC25 / UC26 (KI-01), DEV-ALL-50 — Kế toán, Admin quản lý công đoạn và nhóm công đoạn của mã hàng. Riêng Kế toán đặt đơn giá
 * ngay trên từng công đoạn (có ngày hiệu lực, gửi Admin duyệt). Tích nhiều dòng để xếp nhóm một lần.
 */
export function OperationsPage() {
  const canManageRates = useOperationRole() === 'accountant';
  const { productId, selected, setSelected, filters, pagination, changeProduct, changeFilter } =
    useOperationBoard();
  const [editing, setEditing] = useState<Operation | null>(null);
  const [rating, setRating] = useState<Operation | null>(null);
  const [viewing, setViewing] = useState<Operation | null>(null);
  const [creating, setCreating] = useState(false);
  /** Công đoạn vừa thêm: mở luôn ô nhập đơn giá khi danh sách đã tải lại có nó. */
  const [pendingRateId, setPendingRateId] = useState<number | null>(null);
  const [importOpen, setImportOpen] = useState(false);
  const [groupsOpen, setGroupsOpen] = useState(false);
  const operations = useOperations(productId);
  const groups = useOperationGroups(productId);
  const products = useProductLookup();
  const toggle = useToggleOperation();
  const submit = useSubmitPieceRate();

  const all = operations.data ?? [];
  const groupList = groups.data ?? [];
  const ratingTarget =
    rating ??
    (pendingRateId === null ? null : (all.find((o) => o.operationId === pendingRateId) ?? null));
  /** Dòng tóm tắt chỉ tính công đoạn đang dùng; số trên bộ lọc theo trạng thái đang chọn để khớp số dòng hiện ra. */
  const activeCounts = countOperations(filterOperations(all, { status: 'Active' }));
  const counts = countOperations(filterOperations(all, { status: filters.status }));
  const product = products.data?.items.find((p) => p.productId === productId);
  const productLabel = product ? `${product.productCode} - ${product.productName}` : '';
  const showNoFinalHint =
    operations.isSuccess &&
    activeCounts.active > 0 &&
    !all.some((o) => o.isFinalStep && o.status === 'Active');
  const visible = filterOperations(all, filters);

  const columns = operationColumns({
    onView: setViewing,
    onEdit: setEditing,
    onSetRate: setRating,
    onSubmitRate: (pieceRateId) => submit.mutate(pieceRateId),
    onToggle: (row, discontinue) => toggle.mutate({ id: row.operationId, discontinue }),
    canManageRates,
  });

  return (
    <PageCard
      title={text.title}
      description={canManageRates ? text.subtitle : text.adminSubtitle}
      extra={
        <OperationPageActions
          disabled={productId === null}
          onManageGroups={() => setGroupsOpen(true)}
          onImport={() => setImportOpen(true)}
          onAdd={() => setCreating(true)}
        />
      }
    >
      <OperationFilterBar
        productId={productId}
        onProductChange={changeProduct}
        filter={filters}
        onFilterChange={changeFilter}
        groups={groupList}
        counts={counts}
      />

      {showNoFinalHint && <Alert type="info" showIcon message={text.noFinalStep} />}
      {all.length > 0 && (
        <Typography.Text type="secondary">
          {fill(text.summary, activeCounts.active, activeCounts.ungrouped, activeCounts.missing)}
        </Typography.Text>
      )}

      <QueryError error={operations.error} onRetry={() => void operations.refetch()} />
      <QueryError error={groups.error} onRetry={() => void groups.refetch()} />

      {productId === null ? (
        <Empty description={text.selectProductHint} style={{ margin: '48px 0' }} />
      ) : (
        <>
          <OperationTable
            productId={productId}
            columns={columns}
            rows={visible}
            loading={operations.isFetching}
            pagination={pagination(visible.length)}
            selected={selected}
            onSelect={setSelected}
            groups={groupList}
            draftRateIds={canManageRates ? draftRateIdsOf(all, selected) : []}
          />
        </>
      )}

      <PieceRateHistoryModal operation={viewing} onClose={() => setViewing(null)} />

      {productId !== null && (
        <>
          <OperationFormModal
            open={editing !== null || creating}
            productId={productId}
            operation={editing}
            groups={groupList.map((g) => g.groupName)}
            hasOtherFinal={all.some(
              (o) =>
                o.isFinalStep && o.status === 'Active' && o.operationId !== editing?.operationId,
            )}
            defaults={nextOperationDefaults(all)}
            onClose={() => {
              setEditing(null);
              setCreating(false);
            }}
            onCreated={canManageRates ? setPendingRateId : undefined}
          />
          {canManageRates && (
            <PieceRateFormModal
              operation={ratingTarget}
              onClose={() => {
                setRating(null);
                setPendingRateId(null);
              }}
            />
          )}
          {importOpen && (
            <OperationImportModal
              open
              productId={productId}
              productLabel={productLabel}
              allowRates={canManageRates}
              onClose={() => setImportOpen(false)}
            />
          )}
          {groupsOpen && (
            <OperationGroupModal
              productId={productId}
              productLabel={productLabel}
              onClose={() => setGroupsOpen(false)}
            />
          )}
        </>
      )}
    </PageCard>
  );
}
