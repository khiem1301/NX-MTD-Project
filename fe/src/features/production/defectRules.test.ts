import { canCancelDefect, canConfirmDefect, defectStateOf, isObjectionOpen } from './defectRules';

const base = { status: 'Recorded' as const, workerObjection: null, canEdit: true };

describe('defectStateOf', () => {
  it('phiếu mới ghi là đã ghi', () => {
    expect(defectStateOf(base)).toBe('Pending');
  });

  it('công nhân đã phản đối mà phiếu chưa được quyết định thì chờ quyết định', () => {
    expect(defectStateOf({ ...base, workerObjection: 'sai người' })).toBe('ObjectionPending');
  });

  it('trạng thái đã chấp nhận, đã hủy, đã khóa đứng trước việc có phản đối', () => {
    expect(defectStateOf({ ...base, status: 'Confirmed', workerObjection: 'sai' })).toBe(
      'Confirmed',
    );
    expect(defectStateOf({ ...base, status: 'Cancelled', workerObjection: 'sai' })).toBe(
      'Cancelled',
    );
  });
});

describe('thao tác trên phiếu', () => {
  it('chỉ chấp nhận được phiếu đã ghi mà công nhân đã phản đối', () => {
    expect(canConfirmDefect(base)).toBe(false);
    expect(canConfirmDefect({ ...base, workerObjection: 'sai' })).toBe(true);
    expect(canConfirmDefect({ ...base, status: 'Confirmed', workerObjection: 'sai' })).toBe(false);
    expect(canConfirmDefect({ ...base, status: 'Cancelled', workerObjection: 'sai' })).toBe(false);
  });

  it('phiếu đã chấp nhận hay đã hủy không hủy được nữa', () => {
    expect(canCancelDefect({ ...base, status: 'Confirmed' }, true)).toBe(false);
    expect(canCancelDefect({ ...base, status: 'Cancelled' }, true)).toBe(false);
  });

  it('tổ trưởng chỉ hủy khi còn hạn, Quản đốc hủy được kể cả quá hạn', () => {
    expect(canCancelDefect({ ...base, canEdit: false }, false)).toBe(false);
    expect(canCancelDefect({ ...base, canEdit: false }, true)).toBe(true);
    expect(canCancelDefect(base, false)).toBe(true);
  });
});

describe('isObjectionOpen', () => {
  const recordedAt = '2026-11-01T08:00:00';
  const at = (hours: number, minutes = 0) =>
    new Date(new Date(recordedAt).getTime() + (hours * 60 + minutes) * 60_000);

  it('còn hạn tới đúng 48 giờ, quá 1 phút thì hết hạn', () => {
    expect(isObjectionOpen({ recordedAt }, at(0))).toBe(true);
    expect(isObjectionOpen({ recordedAt }, at(48))).toBe(true);
    expect(isObjectionOpen({ recordedAt }, at(48, 1))).toBe(false);
  });
});
