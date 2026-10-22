import { ROLES } from '@/constants/roles';
import { ROUTES } from '@/constants/routes';
import type { MenuEntry } from '@/types/menu';

const { Admin, Manager, WarehouseKeeper, TeamLeader, TeamDeputy } = ROLES;

export const warehouseMenu: MenuEntry[] = [
  { key: 'materials', label: 'Danh mục NVL', path: ROUTES.materials, roles: [WarehouseKeeper] },
  {
    key: 'order-materials',
    label: 'NVL của đơn',
    path: ROUTES.orderMaterials,
    roles: [Admin, Manager],
  },
  {
    key: 'material-receipts',
    label: 'Phiếu nhập NVL',
    path: ROUTES.materialReceipts,
    roles: [WarehouseKeeper, Manager],
  },
  {
    key: 'material-issues',
    label: 'Phiếu cấp NVL / BTP',
    path: ROUTES.materialIssues,
    roles: [WarehouseKeeper, TeamLeader, TeamDeputy],
  },
  {
    key: 'piece-holdings',
    label: 'BTP tổ đang giữ',
    path: ROUTES.pieceHoldings,
    roles: [TeamLeader, TeamDeputy],
  },
  {
    key: 'inventories',
    label: 'Tồn kho',
    path: ROUTES.inventories,
    roles: [WarehouseKeeper, Manager, Admin],
  },
];
