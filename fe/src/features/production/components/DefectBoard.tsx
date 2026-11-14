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
import { DefectEditModal } from './DefectEditModal';
import { PRODUCTION_TEXT } from '../constants';
import { useCancelDefect, useDefects } from '../hooks/useDefects';
import type { Defect, DefectFilters, OutputRole } from '../types';
import { PageCard } from '@/components/common/PageCard';
import { DataTable } from '@/components/common/DataTable';
import { defectColumns } from './defectColumns';

const text = PRODUCTION_TEXT.defects;

/**
 * UC56 (KI-08) — phiếu hàng hỏng của tổ. Tổ trưởng / tổ phó ghi, sửa và hủy phiếu trong 24 giờ; Quản đốc và Admin xem các tổ và
 * sửa / hủy phiếu ghi sai kể cả sau 24 giờ khi chưa vướng ràng buộc (hủy bắt buộc lý do, có nhật ký).
 */
export function DefectBoard({ role }: { role: OutputRole }) {
  const isManager = role === 'manager';
  const table = useTableQuery<DefectFilters>({});
  const [formOpen, setFormOpen] = useState(false);
  const [cancelling, setCancelling] = useState<Defect | null>(null);
  const [editing, setEditing] = useState<Defect | null>(null);
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

  const columns = defectColumns({
    isManager,
    onReplace: setReplacing,
    onCancel: setCancelling,
    onEdit: setEditing,
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
      <DefectEditModal role={role} defect={editing} onClose={() => setEditing(null)} />
    </PageCard>
  );
}
