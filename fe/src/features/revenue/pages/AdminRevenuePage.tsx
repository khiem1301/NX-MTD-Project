import { RevenueBoard } from '../components/RevenueBoard';

/** Route Admin — api/v1/admin/revenue (GBR-02: Admin cũng xem được doanh thu). */
export function AdminRevenuePage() {
  return <RevenueBoard scope="admin" />;
}
