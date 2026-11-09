import { render, screen } from '@testing-library/react';
import { FabricCheckAlerts } from './FabricCheckAlerts';
import type { WorkOrderFabricCheck } from '../types';

const check = (
  materialId: number,
  status: WorkOrderFabricCheck['status'],
  message: string | null,
): WorkOrderFabricCheck => ({
  materialId,
  materialCode: 'V01',
  materialName: 'Vải',
  materialColor: '',
  status,
  needMeters: 100,
  availableQty: 50,
  message,
});

describe('FabricCheckAlerts', () => {
  it('đủ vải thì không hiện gì', () => {
    const { container } = render(<FabricCheckAlerts checks={[check(1, 'Enough', null)]} />);

    expect(container).toBeEmptyDOMElement();
  });

  it('thiếu vải và bìa lệch tỉ lệ hiện ở hai cảnh báo riêng; chỉ phần thiếu vải kèm ghi chú ước tính', () => {
    render(
      <FabricCheckAlerts
        checks={[
          check(1, 'Short', 'thiếu 50 m'),
          check(2, 'Enough', 'Bìa 1A lệch tỉ lệ lệnh, dư 15 sản phẩm'),
        ]}
      />,
    );

    expect(screen.getByText('Không đủ vải cho lệnh cắt')).toBeInTheDocument();
    expect(screen.getByText('thiếu 50 m')).toBeInTheDocument();
    expect(screen.getByText('Bìa lệch tỉ lệ lệnh cắt')).toBeInTheDocument();
    expect(screen.getByText('Bìa 1A lệch tỉ lệ lệnh, dư 15 sản phẩm')).toBeInTheDocument();
    expect(screen.getAllByText(/Lệnh vẫn lưu được/)).toHaveLength(1);
    expect(screen.queryByText('Chưa tính được vải')).not.toBeInTheDocument();
  });

  it('chưa tính được thì hiện riêng, không gọi là thiếu vải', () => {
    render(<FabricCheckAlerts checks={[check(1, 'Unknown', 'chưa nhập tỉ lệ')]} />);

    expect(screen.getByText('Chưa tính được vải')).toBeInTheDocument();
    expect(screen.queryByText('Không đủ vải cho lệnh cắt')).not.toBeInTheDocument();
  });
});
