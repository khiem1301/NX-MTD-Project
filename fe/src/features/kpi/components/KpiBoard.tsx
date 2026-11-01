import { useState } from 'react';
import { KpiTargetFormModal } from './KpiTargetFormModal';
import { KpiTargetTable } from './KpiTargetTable';
import { KPI_TEXT } from '../constants';
import type { KpiTarget, KpiTargetFilter } from '../types';
import { PageCard } from '@/components/common/PageCard';

const DEFAULT_FILTER: KpiTargetFilter = { page: 1, pageSize: 20 };

/**
 * UC19 / UC21 (MI-17) — chỉ tiêu KPI tổ may kèm thực tế vs chỉ tiêu; chỉ chốt khi kết thúc đơn của tổ. Quản đốc và Admin
 * lập / sửa / hủy chỉ tiêu, đặt KPI theo ngày trên lịch và kết thúc đơn.
 */
export function KpiBoard() {
  const [filter, setFilter] = useState<KpiTargetFilter>(DEFAULT_FILTER);
  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<KpiTarget | null>(null);

  const openForm = (target: KpiTarget | null) => {
    setEditing(target);
    setFormOpen(true);
  };

  return (
    <PageCard title={KPI_TEXT.listPageTitle} description={KPI_TEXT.listPageSubtitle}>
      <KpiTargetTable
        filter={filter}
        onFilterChange={setFilter}
        onAdd={() => openForm(null)}
        onEdit={openForm}
      />
      <KpiTargetFormModal open={formOpen} target={editing} onClose={() => setFormOpen(false)} />
    </PageCard>
  );
}
