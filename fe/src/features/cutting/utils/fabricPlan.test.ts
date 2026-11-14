import { fabricPlanLine, markerUseLabel, planLabel, ratioLabel } from './fabricPlan';
import type { FabricMarkerPlan, FabricMarkerUse, WorkOrderFabricCheck } from '../types';

const use: FabricMarkerUse = {
  markerCode: '1A',
  lengthM: 2.2,
  plies: 10,
  meters: 22,
  ratio: [
    { sizeCode: 'M', piecesPerPly: 1 },
    { sizeCode: 'L', piecesPerPly: 2 },
    { sizeCode: 'XL', piecesPerPly: 1 },
  ],
};
const plan: FabricMarkerPlan = {
  markerNo: 1,
  isExtra: false,
  meters: 22,
  surplus: 0,
  isProvenOptimal: true,
  markers: [use],
};

describe('fabricPlan', () => {
  it('tỉ lệ size ghi số sản phẩm trước, size sau, theo thứ tự máy chủ trả', () => {
    expect(ratioLabel(use)).toBe('1M, 2L, 1XL');
    expect(markerUseLabel(use)).toBe('1A (1M, 2L, 1XL) × 10 lớp');
  });

  it('vải rộng gấp k lần bìa ghi số bìa song song trên một lớp', () => {
    expect(markerUseLabel({ ...use, across: 2, plies: 8 })).toBe(
      '1A (1M, 2L, 1XL) × 2 bìa song song × 8 lớp',
    );
    expect(markerUseLabel({ ...use, across: 1 })).toBe('1A (1M, 2L, 1XL) × 10 lớp');
  });

  it('nhóm bìa ghi các bìa, phần dư và ghi chú chưa chắc tối ưu; cắt lẻ / cắt bù ghi riêng', () => {
    expect(planLabel(plan)).toBe('Nhóm bìa 1: 1A (1M, 2L, 1XL) × 10 lớp');
    expect(
      planLabel({ ...plan, isExtra: true, markerNo: 2, surplus: 1, isProvenOptimal: false }),
    ).toBe(
      'Cắt lẻ / cắt bù, nhóm bìa 2: 1A (1M, 2L, 1XL) × 10 lớp (dư 1) (chưa chắc là ít mét nhất)',
    );
  });

  it('dòng của vải gộp các nhóm bìa; vải chưa tính được thì không có dòng', () => {
    const check = {
      materialId: 1,
      materialCode: 'V01',
      materialName: 'Vải',
      materialColor: 'Đen',
      status: 'Enough',
      needMeters: 38,
      availableQty: 50,
      message: null,
      markerPlans: [
        plan,
        {
          ...plan,
          markerNo: 2,
          markers: [{ ...use, markerCode: '2A', ratio: [{ sizeCode: 'M', piecesPerPly: 2 }] }],
        },
      ],
      warehouseQty: 50,
      holdingQty: 0,
    } satisfies WorkOrderFabricCheck;

    expect(fabricPlanLine(check)).toBe(
      'V01 Vải (Đen): cần khoảng 38 m. Nhóm bìa 1: 1A (1M, 2L, 1XL) × 10 lớp; Nhóm bìa 2: 2A (2M) × 10 lớp',
    );
    expect(
      fabricPlanLine({ ...check, status: 'Unknown', needMeters: null, markerPlans: null }),
    ).toBeNull();
  });
});
