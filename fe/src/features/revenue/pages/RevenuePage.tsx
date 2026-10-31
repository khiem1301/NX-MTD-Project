import { useState } from 'react';
import { RevenueTable } from '../components/RevenueTable';
import { REVENUE_TEXT } from '../constants';
import type { RevenueScope } from '../api/revenueApi';
import type { RevenueFilter } from '../types';
import { PageCard } from '@/components/common/PageCard';

const DEFAULT_FILTER: RevenueFilter = { page: 1, pageSize: 20 };

type Props = { scope: RevenueScope };

/** UC27 — Doanh thu: Kế toán + Admin đều xem được (GBR-02), route khác nhau theo role nhưng cùng màn. */
export function RevenuePage({ scope }: Props) {
  const [filter, setFilter] = useState<RevenueFilter>(DEFAULT_FILTER);

  return (
    <PageCard title={REVENUE_TEXT.pageTitle} description={REVENUE_TEXT.pageSubtitle}>
      <RevenueTable scope={scope} filter={filter} onFilterChange={setFilter} />
    </PageCard>
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
