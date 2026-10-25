import { useState } from 'react';
import { Menu } from 'antd';
import type { NavItem } from './menuModel';

type Props = {
  items: NavItem[];
  selectedPath: string | undefined;
  activeGroupKey: string | undefined;
  collapsed: boolean;
  onNavigate: (path: string) => void;
};

/** Menu thanh bên: mỗi lần chỉ mở một nhóm, tự mở nhóm chứa trang đang xem. */
export function SideMenu({ items, selectedPath, activeGroupKey, collapsed, onNavigate }: Props) {
  // Người dùng tự mở/đóng nhóm thì nhớ cho tới khi sang nhóm khác; từ đó tự mở nhóm của trang mới.
  const [manual, setManual] = useState<{ at: string | undefined; keys: string[] } | null>(null);
  const openKeys =
    manual !== null && manual.at === activeGroupKey ? manual.keys : activeGroupKey ? [activeGroupKey] : [];

  return (
    <Menu
      theme="dark"
      mode="inline"
      inlineCollapsed={collapsed}
      selectedKeys={selectedPath ? [selectedPath] : []}
      openKeys={collapsed ? undefined : openKeys}
      onOpenChange={(keys) => {
        if (collapsed) return;
        const opened = keys.find((k) => !openKeys.includes(k));
        setManual({ at: activeGroupKey, keys: opened ? [opened] : keys });
      }}
      items={items}
      onClick={({ key }) => onNavigate(key)}
    />
  );
}
