import { useState } from 'react';
import { Button, Space } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import type { Dayjs } from 'dayjs';
import { DebouncedSearch } from '@/components/common/DebouncedSearch';
import { QueryError } from '@/components/common/QueryError';
import { ReasonModal } from '@/components/common/ReasonModal';
import { toApiDate } from '@/utils/format';
import { DefectFormModal } from './DefectFormModal';
import { ReplacementCuttingModal } from './ReplacementCuttingModal';
import { PRODUCTION_TEXT } from '../constants';
import { useCancelDefect, useDefects } from '../hooks/useProduction';
import type { Defect, DefectQuery, OutputRole } from '../types';
import { PageCard } from '@/components/common/PageCard';
import { DataTable } from '@/components/common/DataTable';
import { AppDatePicker } from '@/components/common/DateInputs';
import { defectColumns } from './defectColumns';

const text = PRODUCTION_TEXT.defects;

/**
 * UC56 (KI-08) — phiếu hàng hỏng của tổ. Tổ trưởng / tổ phó ghi và hủy phiếu trong hạn; Quản đốc xem các tổ
 * và hủy phiếu ghi sai, kể cả sau hạn (bắt buộc lý do, có nhật ký).
 */
export function DefectBoard({ role }: { role: OutputRole }) {
  const isManager = role === 'manager';
  const [query, setQuery] = useState<DefectQuery>({ page: 1, pageSize: 20 });
  const [formOpen, setFormOpen] = useState(false);
  const [cancelling, setCancelling] = useState<Defect | null>(null);
  const [replacingPick, setReplacing] = useState<Defect | null>(null);
  // Vừa ghi hàng hủy (Scrap): mở luôn đơn cắt bù cho Quản đốc (swimlane III.3.9–III.3.10); phiếu lấy từ danh sách sau khi tải lại.
  const [autoReplaceId, setAutoReplaceId] = useState<number | null>(null);
  const defects = useDefects(role, query);
  const replacing =
    replacingPick ??
    (autoReplaceId === null
      ? null
      : (defects.data?.items.find((d) => d.defectRecordId === autoReplaceId) ?? null));
  const cancel = useCancelDefect(role);

  const columns = defectColumns({ isManager, onReplace: setReplacing, onCancel: setCancelling });

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
      <Space wrap>
        <DebouncedSearch
          allowClear
          style={{ width: 320 }}
          placeholder={text.searchPlaceholder}
          onSearch={(keyword) =>
            setQuery({ ...query, page: 1, keyword: keyword.trim() || undefined })
          }
        />
        <AppDatePicker
          allowClear
          placeholder={text.filterDate}
          onChange={(v: Dayjs | null) =>
            setQuery({ ...query, page: 1, workDate: v ? toApiDate(v) : undefined })
          }
        />
      </Space>

      <QueryError error={defects.error} onRetry={() => void defects.refetch()} />

      <DataTable<Defect>
        rowKey="defectRecordId"
        size="small"
        columns={columns}
        dataSource={defects.data?.items}
        loading={defects.isFetching}
        scroll={{ x: 'max-content' }}
        pagination={{
          current: defects.data?.page ?? query.page,
          pageSize: defects.data?.pageSize ?? query.pageSize,
          total: defects.data?.totalItems ?? 0,
          showSizeChanger: true,
          pageSizeOptions: [10, 20, 50],
          showTotal: (total, [from, to]) => `${from}–${to} / ${total} dòng`,
          onChange: (page, pageSize) => setQuery({ ...query, page, pageSize }),
        }}
      />

      {!isManager && (
        <DefectFormModal
          open={formOpen}
          onClose={() => setFormOpen(false)}
          onCreated={(id, type) => type === 'Scrap' && setAutoReplaceId(id)}
        />
      )}
      {!isManager && (
        <ReplacementCuttingModal
          defect={replacing}
          onClose={() => {
            setReplacing(null);
            setAutoReplaceId(null);
          }}
        />
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
    </PageCard>
  );
}
