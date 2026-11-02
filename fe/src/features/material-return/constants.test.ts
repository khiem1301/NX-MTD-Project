import { qtyDecimalsRule } from './constants';

describe('qtyDecimalsRule', () => {
  it.each([40, 0.001, 12.345, 40.123, null, undefined])('nhận %p', async (value) => {
    await expect(qtyDecimalsRule.validator(null, value)).resolves.toBeUndefined();
  });

  it('chặn quá 3 số lẻ', async () => {
    await expect(qtyDecimalsRule.validator(null, 1.2345)).rejects.toThrow('Tối đa 3 số lẻ.');
  });
});
