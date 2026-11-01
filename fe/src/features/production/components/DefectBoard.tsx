import { useState } from 'react';
import { Button } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import { QueryError } from '@/components/common/QueryError';
import { ReasonModal } from '@/components/common/ReasonModal';
import { useTableQuery } from '@/hooks/useTableQuery';
import { DefectExpandedRow } from './DefectExpandedRow';
import { DefectFilterBar } from './DefectFilterBar';
import { DefectFormModal } from './DefectFormModal';
import { ReplacementCuttingModal } from './ReplacementCuttingModal';
import { ConfirmDefectModal } from './ConfirmDefectModal';
import { PRODUCTION_TEXT } from '../constants';
import { useCancelDefect, useConfirmDefect, useDefects } from '../hooks/useProduction';
import type { Defect, DefectFilters, OutputRole } from '../types';
import { PageCard } from '@/components/common/PageCard';
import { DataTable } from '@/components/common/DataTable';
import { defectColumns } from './defectColumns';

const text = PRODUCTION_TEXT.defects;

/**
 * UC56 (KI-08) — phiếu hàng hỏng của tổ. Tổ trưởng / tổ phó ghi và hủy phiếu trong hạn; Quản đốc và Admin xem các tổ và,
 * khi công nhân phản đối, chấp nhận phiếu (giữ phiếu) hoặc hủy phiếu ghi sai, kể cả sau hạn (bắt buộc lý do, có nhật ký).
 */
export function DefectBoard({ role }: { role: OutputRole }) {
  const isManager = role === 'manager';
  const table = useTableQuery<DefectFilters>({});
  const [formOpen, setFormOpen] = useState(false);
  const [cancelling, setCancelling] = useState<Defect | null>(null);
  const [confirming, setConfirming] = useState<Defect | null>(null);
  const [replacingPick, setReplacing] = useState<Defect | null>(null);
  // Vừa ghi hàng hủy (Scrap): mở luôn đơn cắt bù (swimlane III.3.9–III.3.10); phiếu lấy từ danh sách sau khi tải lại.
  const [autoReplaceId, setAutoReplaceId] = useState<number | null>(null);
  const defects = useDefects(role, table.params);
  const replacing =
    replacingPick ??
    (autoReplaceId === null
      ? null
      : (defects.data?.items.find((d) => d.defectRecordId === autoReplaceId) ?? null));
  const cancel = useCancelDefect(role);
  const confirm = useConfirmDefect();

  const columns = defectColumns({
    isManager,
    onReplace: setReplacing,
    onCancel: setCancelling,
    onConfirm: setConfirming,
  });

  return (
    <PageCard
      title={isManager ? text.managerTitle : text.title}
      description={isManager ? text.managerSubtitle : text.subtitle}
      extra={
        !isManager && (
          <Button type="primary" icon={<PlusOutlined />} onClick={() => setFormOpen(true)}>
            {text.add}
          </Button>
        )
      }
    >
      <DefectFilterBar filters={table.filters} isManager={isManager} onChange={table.setFilters} />

      <QueryError error={defects.error} onRetry={() => void defects.refetch()} />

      <DataTable<Defect>
        rowKey="defectRecordId"
        size="small"
        columns={columns}
        dataSource={defects.data?.items}
        loading={defects.isFetching}
        expandable={{ expandedRowRender: (d) => <DefectExpandedRow defect={d} /> }}
        pagination={table.pagination(defects.data?.totalItems)}
      />

      {!isManager && (
        <>
          <DefectFormModal
            open={formOpen}
            onClose={() => setFormOpen(false)}
            onCreated={(id, type) => type === 'Scrap' && setAutoReplaceId(id)}
          />
          <ReplacementCuttingModal
            defect={replacing}
            onClose={() => {
              setReplacing(null);
              setAutoReplaceId(null);
            }}
          />
        </>
      )}
      <ReasonModal
        open={cancelling !== null}
        title={text.cancelTitle}
        loading={cancel.isPending}
        onCancel={() => setCancelling(null)}
        onSubmit={(reason) =>
          cancelling &&
          cancel.mutate(
            { id: cancelling.defectRecordId, body: { reason, rowVer: cancelling.rowVer } },
            { onSuccess: () => setCancelling(null) },
          )
        }
      />
      <ConfirmDefectModal
        defect={confirming}
        loading={confirm.isPending}
        onCancel={() => setConfirming(null)}
        onSubmit={(note) =>
          confirming &&
          confirm.mutate(
            { id: confirming.defectRecordId, body: { note, rowVer: confirming.rowVer } },
            { onSuccess: () => setConfirming(null) },
          )
        }
      />
    </PageCard>
  );
}
