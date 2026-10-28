import {
  CarOutlined,
  ContainerOutlined,
  DollarOutlined,
  FileTextOutlined,
  ScissorOutlined,
  SettingOutlined,
  ShoppingCartOutlined,
  ShoppingOutlined,
  TeamOutlined,
  ToolOutlined,
  UserOutlined,
} from '@ant-design/icons';
import type { MenuGroup } from '@/components/layout/menuModel';

/**
 * Cách gom menu ở thanh bên: nhóm theo nghiệp vụ, mỗi nhóm tham chiếu `MenuEntry.key`.
 * Mục chưa có ở đây tự rơi vào nhóm "Khác" (xem buildNavigation), nên thêm menu mới vẫn hiện;
 * chỉ cần bổ sung key vào đúng nhóm khi muốn xếp gọn.
 */
export const MENU_GROUPS: MenuGroup[] = [
  {
    key: 'orders',
    label: 'Đơn hàng',
    icon: <ShoppingOutlined />,
    entries: ['orders', 'products', 'customers'],
  },
  {
    key: 'purchase',
    label: 'Mua hàng',
    icon: <ShoppingCartOutlined />,
    entries: ['suppliers', 'purchase-orders', 'purchase-payments'],
  },
  {
    key: 'warehouse',
    label: 'Kho',
    icon: <ContainerOutlined />,
    entries: [
      'materials',
      'order-materials',
      'incoming-materials',
      'material-receipts',
      'short-deliveries',
      'material-issues',
      'material-returns',
      'piece-holdings',
      'inventories',
      'stocktakes',
    ],
  },
  {
    key: 'cutting',
    label: 'Cắt',
    icon: <ScissorOutlined />,
    entries: ['work-orders', 'cutting-lays', 'cut-handovers'],
  },
  {
    key: 'production',
    label: 'Sản xuất',
    icon: <ToolOutlined />,
    entries: [
      'operations',
      'admin-piece-rates',
      'operation-assignments',
      'piece-movements',
      'output-records',
      'defect-records',
      'qc-batches',
      'manager-output-records',
      'manager-defect-records',
      'manager-qc-batches',
      'kpi-targets',
      'admin-kpi-targets',
    ],
  },
  {
    key: 'delivery',
    label: 'Giao hàng',
    icon: <CarOutlined />,
    entries: ['fg-handovers', 'dispatch-notes', 'vehicle-log'],
  },
  {
    key: 'hr',
    label: 'Nhân sự',
    icon: <TeamOutlined />,
    entries: [
      'admin-employees',
      'manager-employees',
      'teams',
      'grades',
      'attendance',
      'team-attendance',
      'team-meal',
      'meal-settings',
    ],
  },
  {
    key: 'finance',
    label: 'Tài chính',
    icon: <DollarOutlined />,
    entries: [
      'payroll',
      'payroll-approval',
      'meal-summary',
      'monthly-cost',
      'revenue-accountant',
      'revenue-admin',
      'break-even',
      'cost-summary',
      'accounting-reports',
    ],
  },
  {
    key: 'requests',
    label: 'Đơn từ',
    icon: <FileTextOutlined />,
    entries: ['requests', 'request-inbox'],
  },
  {
    key: 'mine',
    label: 'Của tôi',
    icon: <UserOutlined />,
    entries: [
      'my-attendance',
      'my-payslip',
      'my-estimated-pay',
      'my-piece-movements',
      'my-defects',
    ],
  },
  {
    key: 'system',
    label: 'Hệ thống',
    icon: <SettingOutlined />,
    entries: ['audit-logs', 'notifications', 'profile'],
  },
];

/** Đã có ở header (chuông thông báo, menu người dùng) nên không chiếm chỗ ở thanh bên. */
export const SIDEBAR_HIDDEN = ['notifications', 'profile'] as const;
export const HOME_ENTRY_KEY = 'home';
