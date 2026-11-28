import { ROLES } from '@/constants/roles';
import { PRODUCTION_ROUTES, ROUTES } from '@/constants/routes';
import { isWorkshopRole, mobileShortcuts } from './mobileNav';
import type { NavLeaf } from './menuModel';

const leaf = (path: string): NavLeaf => ({ path, label: path, groupKey: 'g', groupLabel: 'g' });

describe('mobileShortcuts', () => {
  it('tổ trưởng cắt: bỏ mục của tổ may, lấy mục của tổ cắt', () => {
    const leaves = [leaf(ROUTES.workOrders), leaf(ROUTES.cuttingLays)];

    expect(mobileShortcuts(ROLES.TeamLeader, leaves).map((s) => s.path)).toEqual([
      ROUTES.workOrders,
      ROUTES.cuttingLays,
    ]);
  });

  it('nhiều nhất 3 lối tắt', () => {
    const leaves = [
      PRODUCTION_ROUTES.output,
      ROUTES.pieceMovements,
      ROUTES.workOrders,
      ROUTES.cuttingLays,
    ].map(leaf);

    expect(mobileShortcuts(ROLES.TeamDeputy, leaves)).toHaveLength(3);
  });

  it('vai trò văn phòng không có thanh lối tắt', () => {
    expect(isWorkshopRole(ROLES.Admin)).toBe(false);
    expect(mobileShortcuts(ROLES.Admin, [leaf(ROUTES.requests)])).toEqual([]);
  });
});
