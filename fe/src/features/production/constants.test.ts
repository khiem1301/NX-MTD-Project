import { DEFECT_REASON_OPTIONS, SHIFT_OPTIONS, shiftText } from './constants';

describe('shiftText', () => {
  it('nhận cả mã DB "MORNING" và dạng "Morning"', () => {
    expect(shiftText('MORNING')).toBe('Ca sáng');
    expect(shiftText('Afternoon')).toBe('Ca chiều');
    expect(shiftText('OVERTIME')).toBe('Tăng ca');
  });

  it('ca trống hiện dấu gạch, ca lạ giữ nguyên', () => {
    expect(shiftText(null)).toBe('—');
    expect(shiftText('NIGHT')).toBe('NIGHT');
  });
});

describe('danh sách chọn', () => {
  it('đủ 3 ca và 6 lý do hàng hỏng', () => {
    expect(SHIFT_OPTIONS).toHaveLength(3);
    expect(DEFECT_REASON_OPTIONS.map((o) => o.value)).toContain('Other');
    expect(DEFECT_REASON_OPTIONS).toHaveLength(6);
  });
});
