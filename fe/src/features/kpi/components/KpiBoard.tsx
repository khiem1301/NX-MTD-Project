import { useState } from 'react';
import { KpiTargetFormModal } from './KpiTargetFormModal';
import { KpiTargetTable } from './KpiTargetTable';
import { KPI_TEXT } from '../constants';
import type { KpiRole, KpiTarget, KpiTargetFilter } from '../types';
import { PageCard } from '@/components/common/PageCard';

const DEFAULT_FILTER: KpiTargetFilter = { page: 1, pageSize: 20 };

/**
 * UC19 / UC21 (MI-17) — chỉ tiêu KPI tổ may kèm thực tế vs chỉ tiêu; chỉ chốt khi kết thúc đơn của tổ (không xem / chấm theo
 * ngày). Quản đốc lập / sửa / hủy chỉ tiêu và kết thúc đơn; Admin xem cùng dữ liệu nhưng không có nút thao tác.
 */
export function KpiBoard({ role }: { role: KpiRole }) {
  const [filter, setFilter] = useState<KpiTargetFilter>(DEFAULT_FILTER);
  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<KpiTarget | null>(null);
  const isAdmin = role === 'admin';

  const openForm = (target: KpiTarget | null) => {
    setEditing(target);
    setFormOpen(true);
  };

  return (
    <PageCard
      title={isAdmin ? KPI_TEXT.adminPageTitle : KPI_TEXT.listPageTitle}
      description={isAdmin ? KPI_TEXT.adminPageSubtitle : KPI_TEXT.listPageSubtitle}
    >
      <KpiTargetTable
        filter={filter}
        onFilterChange={setFilter}
        role={role}
        onAdd={() => openForm(null)}
        onEdit={openForm}
      />
      {!isAdmin && (
        <KpiTargetFormModal open={formOpen} target={editing} onClose={() => setFormOpen(false)} />
      )}
    </PageCard>
  );
}
