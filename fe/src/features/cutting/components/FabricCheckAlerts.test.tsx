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

  it('thiếu vải và chi tiết rộng hơn khổ hiện ở hai cảnh báo riêng, kèm ghi chú ước tính', () => {
    render(
      <FabricCheckAlerts
        checks={[check(1, 'Short', 'thiếu 50 m'), check(2, 'TooWide', 'tay áo rộng hơn khổ')]}
      />,
    );

    expect(screen.getByText('Không đủ vải cho lệnh cắt')).toBeInTheDocument();
    expect(screen.getByText('thiếu 50 m')).toBeInTheDocument();
    expect(screen.getByText('Chi tiết rộng hơn khổ vải')).toBeInTheDocument();
    expect(screen.getByText('tay áo rộng hơn khổ')).toBeInTheDocument();
    expect(screen.getAllByText(/Lệnh vẫn lưu được/)).toHaveLength(2);
    expect(screen.queryByText('Chưa tính được vải')).not.toBeInTheDocument();
  });

  it('chưa tính được thì hiện riêng, không gọi là thiếu vải', () => {
    render(<FabricCheckAlerts checks={[check(1, 'Unknown', 'chưa nhập tỉ lệ')]} />);

    expect(screen.getByText('Chưa tính được vải')).toBeInTheDocument();
    expect(screen.queryByText('Không đủ vải cho lệnh cắt')).not.toBeInTheDocument();
  });
});
