import type { StatusLabel } from '@/components/common/StatusTag';
import type { AllocationBasis } from './types';

/** Chuỗi hiển thị màn Hòa vốn + giá trị thặng dư (UC11, MI-13, chỉ Admin). */
export const BREAK_EVEN_TEXT = {
  pageTitle: 'Hòa vốn & giá trị thặng dư',
  pageSubtitle:
    'Chỉ tính khi bảng lương tháng đã duyệt và chi phí tháng đã nhập (MSG25). Mỗi lần tính lưu thêm một bộ kết quả mới, không ghi đè lần trước.',
  periodMonth: 'Tháng',
  allocationBasis: 'Cách phân bổ định phí',
  calculate: 'Tính toán',
  viewHistory: 'Xem lịch sử các lần tính',
  viewLatest: 'Chỉ xem lần tính gần nhất',
  export: 'Xuất Excel',
  noDataYet: 'Tháng này chưa có kết quả hòa vốn. Chọn cách phân bổ rồi bấm "Tính toán".',

  allocationBasisLabel: {
    Output: 'Theo sản lượng (OUTPUT)',
    LabourHours: 'Theo giờ công quy đổi (LABOUR_HOURS)',
  },

  table: {
    product: 'Mã hàng',
    allocationBasis: 'Cách phân bổ',
    finishedQty: 'SL hoàn thiện',
    cmtUnitPrice: 'Đơn giá CMT',
    revenue: 'Doanh thu',
    variableCost: 'Biến phí',
    allocatedFixedCost: 'Định phí phân bổ',
    breakEvenQty: 'SL hòa vốn',
    percentReached: '% đạt hòa vốn',
    surplusValue: 'Giá trị thặng dư',
    risk: 'Nguy cơ lỗ',
    calculatedAt: 'Tính lúc',
  },
} as const;

export const ALLOCATION_BASIS_OPTIONS: { value: AllocationBasis; label: string }[] = [
  { value: 'Output', label: BREAK_EVEN_TEXT.allocationBasisLabel.Output },
  { value: 'LabourHours', label: BREAK_EVEN_TEXT.allocationBasisLabel.LabourHours },
];

export const RISK_LABEL: Record<'atRisk' | 'safe', StatusLabel> = {
  atRisk: { text: 'Nguy cơ lỗ', color: 'red' },
  safe: { text: 'An toàn', color: 'green' },
};
