import { canModifyDefect, defectStateOf } from './defectRules';

describe('defectStateOf', () => {
  it('phiếu đã ghi và phiếu cũ đã chốt đều còn hiệu lực', () => {
    expect(defectStateOf({ status: 'Recorded' })).toBe('Active');
    expect(defectStateOf({ status: 'Confirmed' })).toBe('Active');
  });

  it('phiếu đã hủy', () => {
    expect(defectStateOf({ status: 'Cancelled' })).toBe('Cancelled');
  });
});

describe('canModifyDefect', () => {
  it('theo canEdit do BE tính (24 giờ với tổ trưởng, Quản đốc không giới hạn giờ)', () => {
    expect(canModifyDefect({ canEdit: true })).toBe(true);
    expect(canModifyDefect({ canEdit: false })).toBe(false);
  });
});
