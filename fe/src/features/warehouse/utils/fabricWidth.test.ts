import { cmToMeters, formatFabricWidth, metersToCm } from './fabricWidth';

describe('khổ vải', () => {
  it('đổi cm sang m và ngược lại', () => {
    expect(cmToMeters(150)).toBe(1.5);
    expect(metersToCm(1.5)).toBe(150);
  });

  it('không giữ sai số của phép tính số thực', () => {
    expect(metersToCm(1.6)).toBe(160);
    expect(metersToCm(1.15)).toBe(115);
    expect(metersToCm(0.07)).toBe(7);
  });

  it('hiển thị theo mét, chưa có khổ thì ghi gạch ngang', () => {
    expect(formatFabricWidth(150)).toBe('1,5 m');
    expect(formatFabricWidth(null)).toBe('—');
  });

  it('giữ nguyên ô trống', () => {
    expect(cmToMeters(null)).toBeNull();
    expect(cmToMeters(undefined)).toBeNull();
    expect(metersToCm(null)).toBeNull();
  });
});
