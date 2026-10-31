import { useState } from 'react';
import { PageCard } from '@/components/common/PageCard';
import type { RevenueScope } from '../api/revenueApi';
import { REVENUE_TEXT } from '../constants';
import type { RevenueFilter } from '../types';
import { RevenueTable } from './RevenueTable';

const DEFAULT_FILTER: RevenueFilter = { page: 1, pageSize: 20 };

type RevenueBoardProps = { scope: RevenueScope };

/** UC27 — Doanh thu: Kế toán + Admin đều xem được (GBR-02), route khác nhau theo role nhưng cùng màn. */
export function RevenueBoard({ scope }: RevenueBoardProps) {
  const [filter, setFilter] = useState<RevenueFilter>(DEFAULT_FILTER);

  return (
    <PageCard title={REVENUE_TEXT.pageTitle} description={REVENUE_TEXT.pageSubtitle}>
      <RevenueTable scope={scope} filter={filter} onFilterChange={setFilter} />
    </PageCard>
  );
}
