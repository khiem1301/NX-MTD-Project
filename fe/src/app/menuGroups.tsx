import {
  AuditOutlined,
  ClockCircleOutlined,
  ContainerOutlined,
  ControlOutlined,
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
    label: 'Quản lí đơn hàng',
    icon: <ShoppingOutlined />,
    entries: ['orders', 'manager-orders', 'products', 'products-accountant', 'customers'],
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
      'incoming-materials',
      'material-receipts',
      'short-deliveries',
      'material-issues',
      'material-returns',
      'piece-holdings',
      // Thành phẩm: QA/QC bàn giao vào kho rồi lập phiếu xuất giao khách.
      'fg-handovers',
      'dispatch-notes',
      'inventories',
      'nxt-report',
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
    key: 'production-setup',
    label: 'Thiết lập sản xuất',
    icon: <ControlOutlined />,
    entries: ['operations', 'admin-piece-rates', 'order-line-teams', 'kpi-targets'],
  },
  {
    key: 'sewing',
    label: 'Tổ may',
    icon: <ToolOutlined />,
    entries: [
      'operation-assignments',
      'piece-movements',
      'output-records',
      'defect-records',
      'qc-batches',
    ],
  },
  {
    key: 'manager-corrections',
    label: 'Quản đốc sửa',
    icon: <AuditOutlined />,
    entries: ['manager-output-records', 'manager-defect-records', 'manager-qc-batches'],
  },
  {
    key: 'hr',
    label: 'Nhân sự',
    icon: <TeamOutlined />,
    entries: ['admin-employees', 'manager-employees', 'teams', 'grades'],
  },
  {
    key: 'attendance-meals',
    label: 'Chấm công & suất ăn',
    icon: <ClockCircleOutlined />,
    entries: [
      'attendance',
      'team-attendance',
      'team-meal',
      'my-meal',
      'meal-summary',
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
      'monthly-cost',
      'revenue-accountant',
      'revenue-admin',
      'break-even',
      'cost-summary',
      'accounting-reports',
      'vehicle-log',
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
