import { useState } from 'react';
import { Space, Typography } from 'antd';
import { KpiCalendarDrawer } from '../components/KpiCalendarDrawer';
import { KpiTargetFormModal } from '../components/KpiTargetFormModal';
import { KpiTargetTable } from '../components/KpiTargetTable';
import { KPI_TEXT } from '../constants';
import type { KpiRole, KpiTarget, KpiTargetFilter } from '../types';

const DEFAULT_FILTER: KpiTargetFilter = { page: 1, pageSize: 20 };

/**
 * UC19 / UC21 (MI-17) — chỉ tiêu KPI tổ may kèm thực tế vs chỉ tiêu và kết quả từng ngày (MI-18). Quản đốc lập / sửa /
 * hủy chỉ tiêu; Admin xem cùng dữ liệu nhưng không có nút thao tác.
 */
function KpiBoard({ role }: { role: KpiRole }) {
  const [filter, setFilter] = useState<KpiTargetFilter>(DEFAULT_FILTER);
  const [viewingDailyId, setViewingDailyId] = useState<number | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<KpiTarget | null>(null);
  const isAdmin = role === 'admin';

  const openForm = (target: KpiTarget | null) => {
    setEditing(target);
    setFormOpen(true);
  };

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <div>
        <Typography.Title level={3} style={{ marginBottom: 4 }}>
          {isAdmin ? KPI_TEXT.adminPageTitle : KPI_TEXT.listPageTitle}
        </Typography.Title>
        <Typography.Text type="secondary">
          {isAdmin ? KPI_TEXT.adminPageSubtitle : KPI_TEXT.listPageSubtitle}
        </Typography.Text>
      </div>

      <KpiTargetTable
        filter={filter}
        onFilterChange={setFilter}
        onViewDaily={setViewingDailyId}
        role={role}
        onAdd={() => openForm(null)}
        onEdit={openForm}
      />
      <KpiCalendarDrawer kpiTargetId={viewingDailyId} onClose={() => setViewingDailyId(null)} role={role} />
      {!isAdmin && <KpiTargetFormModal open={formOpen} target={editing} onClose={() => setFormOpen(false)} />}
    </Space>
  );
}

/** Quản đốc: lập / sửa / hủy chỉ tiêu và xem kết quả. */
export const KpiTargetListPage = () => <KpiBoard role="manager" />;

/** Admin: chỉ xem chỉ tiêu và kết quả (UC21). */
export const AdminKpiTargetPage = () => <KpiBoard role="admin" />;
