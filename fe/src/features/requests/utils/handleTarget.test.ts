import { getHandleUrl } from './handleTarget';

describe('getHandleUrl', () => {
  it('Xin NVL: Thủ kho sang màn phiếu cấp, kèm mã đơn và loại đơn', () => {
    expect(getHandleUrl('MaterialRequisition', 12, 'WarehouseKeeper')).toBe(
      '/warehouse/material-issues?fromRequest=12&requestType=MaterialRequisition',
    );
  });

  it('Cắt bù: Quản đốc sang màn lệnh cắt', () => {
    expect(getHandleUrl('ReplacementCutting', 7, 'Manager')).toBe(
      '/cutting/work-orders?fromRequest=7&requestType=ReplacementCutting',
    );
  });

  it('Chấm công bù: Kế toán sang màn chấm công', () => {
    expect(getHandleUrl('AttendanceCorrection', 3, 'Accountant')).toBe(
      '/attendance?fromRequest=3&requestType=AttendanceCorrection',
    );
  });

  it('vai trò không xử lý được ở màn đích thì không có link', () => {
    expect(getHandleUrl('MaterialRequisition', 12, 'Accountant')).toBeNull();
    expect(getHandleUrl('MaterialRequisition', 12, undefined)).toBeNull();
  });

  it('loại đơn không có màn đích thì không có link', () => {
    expect(getHandleUrl('SpareParts', 1, 'Admin')).toBeNull();
  });
});
