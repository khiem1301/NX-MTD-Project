import type { ReactNode } from 'react';
import { createPortal } from 'react-dom';
import {
  AppstoreOutlined,
  CheckSquareOutlined,
  FileTextOutlined,
  HomeOutlined,
  ProfileOutlined,
} from '@ant-design/icons';
import { ROUTES } from '@/constants/routes';
import { MOBILE_NAV_LABEL, type MobileShortcut } from './mobileNav';

type Props = {
  shortcuts: MobileShortcut[];
  activePath: string | undefined;
  onNavigate: (path: string) => void;
  onOpenMenu: () => void;
};

/** Biểu tượng theo vị trí lối tắt (nhiều nhất 3). */
const SHORTCUT_ICONS = [
  <CheckSquareOutlined key="first" />,
  <ProfileOutlined key="second" />,
  <FileTextOutlined key="third" />,
];

/** Thanh lối tắt ở đáy màn hình điện thoại: việc hay làm bấm một lần là tới, còn lại nằm trong Menu. */
export function MobileTabBar({ shortcuts, activePath, onNavigate, onOpenMenu }: Props) {
  const tab = (key: string, label: string, icon: ReactNode, onClick: () => void) => (
    <button
      key={key}
      type="button"
      className="mobile-tabbar__item"
      aria-current={key === activePath ? 'page' : undefined}
      onClick={onClick}
    >
      <span className="mobile-tabbar__icon">{icon}</span>
      <span>{label}</span>
    </button>
  );

  // Gắn thẳng vào body: không phụ thuộc layout bên trong nên luôn bám đúng mép dưới màn hình.
  return createPortal(
    <nav className="mobile-tabbar" aria-label={MOBILE_NAV_LABEL.aria}>
      {tab(ROUTES.home, MOBILE_NAV_LABEL.home, <HomeOutlined />, () => onNavigate(ROUTES.home))}
      {shortcuts.map((item, position) =>
        tab(item.path, item.label, SHORTCUT_ICONS[position], () => onNavigate(item.path)),
      )}
      {tab('menu', MOBILE_NAV_LABEL.menu, <AppstoreOutlined />, onOpenMenu)}
    </nav>,
    document.body,
  );
}
