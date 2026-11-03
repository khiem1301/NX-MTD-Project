import type { ReactNode } from 'react';
import type { TableColumnsType } from 'antd';
import { fireEvent, render, screen } from '@testing-library/react';
import { MESSAGES } from '@/constants/messages';
import { LABELS } from '../constants';
import type { OrderMaterial } from '../types';
import { OrderMaterialsTab } from './OrderMaterialsTab';

jest.mock('../hooks/useOrderMaterials', () => ({
  useOrderMaterials: () => ({ data: [{ orderMaterialId: 1 }] }),
  useDeleteOrderMaterial: () => ({ mutate: jest.fn() }),
}));
jest.mock('./OrderMaterialFormModal', () => ({ OrderMaterialFormModal: () => null }));
jest.mock('./OrderMaterialBulkEditor', () => ({ OrderMaterialBulkEditor: () => null }));
jest.mock('./OrderMaterialByProductModal', () => ({
  OrderMaterialByProductModal: () => <span>hộp thêm theo mã hàng</span>,
}));
jest.mock('./OrderMaterialTable', () => ({
  OrderMaterialTable: ({
    actions,
    columns,
  }: {
    actions: ReactNode;
    columns: TableColumnsType<OrderMaterial>;
  }) => (
    <>
      {actions}
      {columns.map((c) => (
        <span key={String(c.key ?? c.title)}>{String(c.title)}</span>
      ))}
    </>
  ),
}));

describe('OrderMaterialsTab', () => {
  it('đơn còn mở thì thêm theo mã hàng, thêm NVL ngoài, sửa cả đơn và có cột thao tác', () => {
    render(<OrderMaterialsTab orderId={5} isClosed={false} />);

    expect(
      screen.getByRole('button', { name: new RegExp(LABELS.addByProduct) }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: new RegExp(LABELS.addExternalMaterial) }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: new RegExp(LABELS.bulkEditOrderMaterials) }),
    ).toBeInTheDocument();
    expect(screen.getByText(MESSAGES.common.actions)).toBeInTheDocument();
  });

  it('bấm thêm theo mã hàng thì mở hộp nhập', () => {
    render(<OrderMaterialsTab orderId={5} isClosed={false} />);

    expect(screen.queryByText('hộp thêm theo mã hàng')).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: new RegExp(LABELS.addByProduct) }));

    expect(screen.getByText('hộp thêm theo mã hàng')).toBeInTheDocument();
  });

  it('đơn đã đóng thì chỉ xem', () => {
    render(<OrderMaterialsTab orderId={5} isClosed />);

    expect(screen.queryByRole('button')).not.toBeInTheDocument();
    expect(screen.queryByText(MESSAGES.common.actions)).not.toBeInTheDocument();
  });
});
