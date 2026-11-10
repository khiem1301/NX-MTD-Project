import { render, screen } from '@testing-library/react';
import { FabricCheckAlerts } from './FabricCheckAlerts';
import type { FabricMarkerPlan, WorkOrderFabricCheck } from '../types';

const plan: FabricMarkerPlan = {
  markerNo: 1,
  isExtra: false,
  meters: 195.3,
  surplus: 16,
  isProvenOptimal: true,
  markers: [
    {
      markerCode: 'A',
      lengthM: 2.1,
      plies: 48,
      meters: 100.8,
      ratio: [
        { sizeCode: 'S', piecesPerPly: 1 },
        { sizeCode: 'L', piecesPerPly: 2 },
      ],
    },
    {
      markerCode: 'B',
      lengthM: 2.8,
      plies: 33,
      meters: 92.4,
      ratio: [
        { sizeCode: 'M', piecesPerPly: 3 },
        { sizeCode: 'L', piecesPerPly: 1 },
      ],
    },
  ],
};

const check = (
  materialId: number,
  status: WorkOrderFabricCheck['status'],
  change: Partial<WorkOrderFabricCheck> = {},
): WorkOrderFabricCheck => ({
  materialId,
  materialCode: 'V01',
  materialName: 'Vải',
  materialColor: '',
  status,
  needMeters: 195.3,
  availableQty: 300,
  message: null,
  markerPlans: [plan],
  warehouseQty: 300,
  holdingQty: 0,
  ...change,
});

describe('FabricCheckAlerts', () => {
  it('đủ vải vẫn hiện khung thông báo số mét cần và bìa nên dùng, kèm tỉ lệ size của bìa', () => {
    const { container } = render(<FabricCheckAlerts checks={[check(1, 'Enough')]} />);

    expect(container.querySelector('.ant-alert-info')).not.toBeNull();
    expect(screen.getByText('Số mét vải cần và bìa nên dùng')).toBeInTheDocument();
    expect(
      screen.getByText(
        'V01 Vải: cần khoảng 195,3 m. Nhóm bìa 1: A (1S, 2L) × 48 lớp, B (3M, 1L) × 33 lớp (dư 16)',
      ),
    ).toBeInTheDocument();
    expect(screen.getByText(/có thể chênh lệch với thực tế/)).toBeInTheDocument();
    expect(screen.queryByText('Không đủ vải cho lệnh cắt')).not.toBeInTheDocument();
  });

  it('thiếu vải hiện khung cảnh báo riêng, vẫn kèm bìa nên dùng', () => {
    const { container } = render(
      <FabricCheckAlerts checks={[check(1, 'Short', { message: 'thiếu 50 m' })]} />,
    );

    expect(container.querySelector('.ant-alert-warning')).not.toBeNull();
    expect(screen.getByText('Không đủ vải cho lệnh cắt')).toBeInTheDocument();
    expect(screen.getByText('thiếu 50 m')).toBeInTheDocument();
    expect(screen.getByText(/A \(1S, 2L\) × 48 lớp/)).toBeInTheDocument();
    expect(screen.getAllByText(/Lệnh vẫn lưu được/)).toHaveLength(1);
  });

  it('chưa tính được thì hiện riêng, không gọi là thiếu vải, không có bìa nên dùng', () => {
    render(
      <FabricCheckAlerts
        checks={[
          check(1, 'Unknown', {
            message: 'chưa nhập bìa',
            needMeters: null,
            availableQty: null,
            markerPlans: null,
          }),
        ]}
      />,
    );

    expect(screen.getByText('Chưa tính được vải')).toBeInTheDocument();
    expect(screen.queryByText('Không đủ vải cho lệnh cắt')).not.toBeInTheDocument();
    expect(screen.queryByText('Số mét vải cần và bìa nên dùng')).not.toBeInTheDocument();
  });

  it('không có dòng kiểm vải thì không hiện gì', () => {
    const { container } = render(<FabricCheckAlerts checks={[]} />);

    expect(container).toBeEmptyDOMElement();
  });
});
