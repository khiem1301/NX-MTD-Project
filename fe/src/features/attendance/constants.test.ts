import { ATTENDANCE_TEXT } from './constants';

describe('chấm công hộ', () => {
  it('có đủ 4 loại chấm bù, khớp mã DB mà BE nhận (UPPER_SNAKE)', () => {
    expect(Object.keys(ATTENDANCE_TEXT.adjust.types).sort()).toEqual(
      ['APPROVED_REQUEST', 'FINGERPRINT_FAIL', 'FORGOT_PUNCH', 'OFFSITE_WORK'].sort(),
    );
  });
});
