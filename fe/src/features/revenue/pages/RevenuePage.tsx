import { useState } from 'react';
import { Space, Typography } from 'antd';
import { RevenueTable } from '../components/RevenueTable';
import { REVENUE_TEXT } from '../constants';
import type { RevenueScope } from '../api/revenueApi';
import type { RevenueFilter } from '../types';

const DEFAULT_FILTER: RevenueFilter = { page: 1, pageSize: 20 };

type Props = { scope: RevenueScope };

/** UC27 — Doanh thu: Kế toán + Admin đều xem được (GBR-02), route khác nhau theo role nhưng cùng màn. */
export function RevenuePage({ scope }: Props) {
  const [filter, setFilter] = useState<RevenueFilter>(DEFAULT_FILTER);

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <div>
        <Typography.Title level={3} style={{ marginBottom: 4 }}>
          {REVENUE_TEXT.pageTitle}
        </Typography.Title>
        <Typography.Text type="secondary">{REVENUE_TEXT.pageSubtitle}</Typography.Text>
      </div>

      <RevenueTable scope={scope} filter={filter} onFilterChange={setFilter} />
    </Space>
  );
}

/** Route Kế toán — api/v1/accountant/revenue. */
export function AccountantRevenuePage() {
  return <RevenuePage scope="accountant" />;
}

/** Route Admin — api/v1/admin/revenue (GBR-02: Admin cũng xem được doanh thu). */
export function AdminRevenuePage() {
  return <RevenuePage scope="admin" />;
}
