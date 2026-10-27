import type { ReactNode } from 'react';
import type { Role } from '@/constants/roles';
import type { MenuEntry } from '@/types/menu';
export { normalizeText } from '@/utils/text';

export type MenuGroup = {
  key: string;
  label: string;
  icon: ReactNode;
  /** Các `MenuEntry.key` thuộc nhóm này, theo thứ tự hiển thị. */
  entries: readonly string[];
};

export type NavLeaf = {
  path: string;
  label: string;
  groupKey: string;
  groupLabel: string;
};

export type NavItem = {
  key: string;
  label: string;
  icon?: ReactNode;
  children?: { key: string; label: string }[];
};

export type Navigation = {
  /** Mục cho antd Menu: nhóm nhiều mục thì gộp thành menu con, nhóm một mục thì hiện thẳng. */
  items: NavItem[];
  /** Mọi chức năng người dùng được vào (kể cả mục ẩn khỏi menu) — dùng cho ô tìm kiếm và tiêu đề trang. */
  leaves: NavLeaf[];
};

export const HOME_PATH = '/';
const OTHER_GROUP: Omit<MenuGroup, 'entries'> = { key: 'other', label: 'Khác', icon: null };

const canSee = (entry: MenuEntry, role: Role | undefined) =>
  entry.roles === 'all' || (role !== undefined && entry.roles.includes(role));

/**
 * Gom các mục menu theo nhóm. Mục chưa được xếp nhóm rơi vào "Khác" để không bao giờ mất chức năng.
 * `hidden` là các mục vẫn tìm được và có tiêu đề nhưng không chiếm chỗ ở thanh bên (đã có ở header).
 */
export function buildNavigation(
  entries: readonly MenuEntry[],
  groups: readonly MenuGroup[],
  role: Role | undefined,
  hidden: readonly string[],
  home: { key: string; icon: ReactNode },
): Navigation {
  const visible = entries.filter((e) => canSee(e, role));
  const byKey = new Map<string, MenuEntry>();
  visible.forEach((e) => byKey.set(e.key, e));

  const claimed = new Set(groups.flatMap((g) => g.entries));
  const orphans = visible.filter((e) => e.key !== home.key && !claimed.has(e.key)).map((e) => e.key);
  const allGroups: MenuGroup[] = orphans.length > 0 ? [...groups, { ...OTHER_GROUP, entries: orphans }] : [...groups];

  const items: NavItem[] = [];
  const leaves: NavLeaf[] = [];

  const homeEntry = byKey.get(home.key);
  if (homeEntry) {
    items.push({ key: homeEntry.path, label: homeEntry.label, icon: home.icon });
    leaves.push({ path: homeEntry.path, label: homeEntry.label, groupKey: home.key, groupLabel: homeEntry.label });
  }

  for (const group of allGroups) {
    const members = group.entries.map((k) => byKey.get(k)).filter((e): e is MenuEntry => e !== undefined);
    members.forEach((m) =>
      leaves.push({ path: m.path, label: m.label, groupKey: group.key, groupLabel: group.label }),
    );
    const shown = members.filter((m) => !hidden.includes(m.key));
    if (shown.length === 0) continue;
    const [only] = shown;
    if (shown.length === 1 && only) {
      items.push({ key: only.path, label: only.label, icon: group.icon });
    } else {
      items.push({
        key: group.key,
        label: group.label,
        icon: group.icon,
        children: shown.map((m) => ({ key: m.path, label: m.label })),
      });
    }
  }
  return { items, leaves };
}

/** Mục đang mở: đường dẫn dài nhất trùng đầu (để trang chi tiết /admin/orders/5 vẫn sáng "Đơn hàng"). */
export function findActiveLeaf(pathname: string, leaves: readonly NavLeaf[]): NavLeaf | undefined {
  let best: NavLeaf | undefined;
  for (const leaf of leaves) {
    const match =
      leaf.path === pathname || (leaf.path !== HOME_PATH && pathname.startsWith(`${leaf.path}/`));
    if (match && (best === undefined || leaf.path.length > best.path.length)) best = leaf;
  }
  return best;
}

export function initials(fullName: string): string {
  const words = fullName.trim().split(/\s+/).filter(Boolean);
  if (words.length === 0) return '?';
  const first = words[0] ?? '';
  const last = words[words.length - 1] ?? '';
  const pick = words.length === 1 ? first.slice(0, 2) : first.charAt(0) + last.charAt(0);
  return pick.toUpperCase();
}
