import { useState } from 'react';
import { Space, Typography } from 'antd';
import { KpiDailyResultDrawer } from '../components/KpiDailyResultDrawer';
import { KpiTargetTable } from '../components/KpiTargetTable';
import { KPI_TEXT } from '../constants';
import type { KpiTargetFilter } from '../types';

const DEFAULT_FILTER: KpiTargetFilter = { page: 1, pageSize: 20 };

/**
 * UC19 — Quản đốc: danh sách chỉ tiêu KPI tổ may; MI-18 "KPI theo ngày" thêm bảng kết quả từng
 * ngày khi xem chi tiết một chỉ tiêu. Lập/sửa/hủy chỉ tiêu và màn "Màn KPI" đầy đủ (chỉ tiêu vs
 * thực tế) thuộc MI-17, ngoài phạm vi màn này.
 */
export function KpiTargetListPage() {
  const [filter, setFilter] = useState<KpiTargetFilter>(DEFAULT_FILTER);
  const [viewingDailyId, setViewingDailyId] = useState<number | null>(null);

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <div>
        <Typography.Title level={3} style={{ marginBottom: 4 }}>
          {KPI_TEXT.listPageTitle}
        </Typography.Title>
        <Typography.Text type="secondary">{KPI_TEXT.listPageSubtitle}</Typography.Text>
      </div>

      <KpiTargetTable filter={filter} onFilterChange={setFilter} onViewDaily={setViewingDailyId} />
      <KpiDailyResultDrawer kpiTargetId={viewingDailyId} onClose={() => setViewingDailyId(null)} />
    </Space>
  );
}
