import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { ROLES, type Role } from '@/constants/roles';
import { OrderMaterialLinkRedirect } from './OrderMaterialLinkRedirect';

let mockRole: Role = ROLES.Manager;
jest.mock('@/hooks/usePermission', () => ({
  usePermission: () => ({ hasRole: (role: Role) => role === mockRole }),
}));

function CurrentUrl() {
  const { pathname, search } = useLocation();
  return <span>{`${pathname}${search}`}</span>;
}

const renderAt = (url: string) =>
  render(
    <MemoryRouter initialEntries={[url]}>
      <Routes>
        <Route path="/warehouse/order-materials" element={<OrderMaterialLinkRedirect />} />
        <Route path="*" element={<CurrentUrl />} />
      </Routes>
    </MemoryRouter>,
  );

describe('OrderMaterialLinkRedirect', () => {
  it('Quản đốc mở thông báo cũ thì tới tab NVL của đơn', () => {
    mockRole = ROLES.Manager;
    renderAt('/warehouse/order-materials?orderId=5');

    expect(screen.getByText('/manager/orders/5?tab=materials')).toBeInTheDocument();
  });

  it('Admin mở thông báo cũ thì tới trang đơn của Admin', () => {
    mockRole = ROLES.Admin;
    renderAt('/warehouse/order-materials?orderId=5');

    expect(screen.getByText('/admin/orders/5?tab=materials')).toBeInTheDocument();
  });

  it('không có đơn thì tới danh sách đơn', () => {
    mockRole = ROLES.Manager;
    renderAt('/warehouse/order-materials');

    expect(screen.getByText('/manager/orders')).toBeInTheDocument();
  });
});
