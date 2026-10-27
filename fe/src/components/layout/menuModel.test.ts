import { ROLES } from '@/constants/roles';
import type { MenuEntry } from '@/types/menu';
import {
  buildNavigation,
  findActiveLeaf,
  initials,
  normalizeText,
  type MenuGroup,
} from './menuModel';

const entries: MenuEntry[] = [
  { key: 'home', label: 'Trang chủ', path: '/', roles: 'all' },
  { key: 'orders', label: 'Đơn hàng', path: '/admin/orders', roles: [ROLES.Admin] },
  { key: 'products', label: 'Mã hàng', path: '/admin/products', roles: [ROLES.Admin] },
  { key: 'requests', label: 'Đơn từ của tôi', path: '/requests', roles: [ROLES.TeamLeader] },
  { key: 'requests-inbox', label: 'Đơn cần xử lý', path: '/requests/inbox', roles: [ROLES.TeamLeader] },
  { key: 'profile', label: 'Hồ sơ của tôi', path: '/profile', roles: 'all' },
  { key: 'new-feature', label: 'Chức năng mới', path: '/new', roles: 'all' },
];
const groups: MenuGroup[] = [
  { key: 'orders', label: 'Đơn hàng', icon: null, entries: ['orders', 'products'] },
  { key: 'req', label: 'Đơn từ', icon: null, entries: ['requests', 'requests-inbox'] },
  { key: 'system', label: 'Hệ thống', icon: null, entries: ['profile'] },
];
const home = { key: 'home', icon: null };

describe('buildNavigation', () => {
  it('gom mục cùng nhóm thành menu con và chỉ giữ mục đúng vai trò', () => {
    const nav = buildNavigation(entries, groups, ROLES.Admin, ['profile'], home);

    expect(nav.items.map((i) => i.key)).toEqual(['/', 'orders', '/new']);
    expect(nav.items[1]?.children?.map((c) => c.key)).toEqual(['/admin/orders', '/admin/products']);
  });

  it('nhóm chỉ còn một mục thì hiện thẳng, không tạo menu con', () => {
    const nav = buildNavigation(entries, groups, ROLES.TeamLeader, ['profile'], home);

    expect(nav.items.map((i) => i.key)).toEqual(['/', 'req', '/new']);
    const single = buildNavigation(entries.filter((e) => e.key !== 'requests-inbox'), groups, ROLES.TeamLeader, ['profile'], home);
    expect(single.items.find((i) => i.key === '/requests')?.children).toBeUndefined();
  });

  it('mục chưa xếp nhóm rơi vào nhóm Khác nên không bị mất', () => {
    const nav = buildNavigation(entries, groups, ROLES.Admin, [], home);

    expect(nav.leaves.find((l) => l.path === '/new')?.groupLabel).toBe('Khác');
  });

  it('mục ẩn khỏi thanh bên vẫn tìm được', () => {
    const nav = buildNavigation(entries, groups, ROLES.Admin, ['profile'], home);

    expect(nav.items.some((i) => i.key === '/profile')).toBe(false);
    expect(nav.leaves.some((l) => l.path === '/profile')).toBe(true);
  });
});

describe('findActiveLeaf', () => {
  const { leaves } = buildNavigation(entries, groups, ROLES.TeamLeader, [], home);

  it('trang chi tiết vẫn sáng mục cha, đường dẫn dài hơn thắng', () => {
    expect(findActiveLeaf('/requests/inbox', leaves)?.path).toBe('/requests/inbox');
    expect(findActiveLeaf('/requests/12', leaves)?.path).toBe('/requests');
  });

  it('trang chủ chỉ khớp đúng "/"', () => {
    expect(findActiveLeaf('/', leaves)?.path).toBe('/');
    expect(findActiveLeaf('/unknown', leaves)).toBeUndefined();
  });
});

describe('normalizeText và initials', () => {
  it('tìm không dấu: "don hang" khớp "Đơn hàng"', () => {
    expect(normalizeText('Đơn hàng')).toContain(normalizeText('don hang'));
  });

  it('chữ cái đầu lấy từ họ và tên cuối', () => {
    expect(initials('Nguyễn Văn An')).toBe('NA');
    expect(initials('Admin')).toBe('AD');
    expect(initials('  ')).toBe('?');
  });
});
