import { colorsWithoutNorm } from './orderNorms';

describe('colorsWithoutNorm', () => {
  it('báo màu mới chưa có định mức, bỏ qua màu đã có (không phân biệt hoa thường)', () => {
    expect(colorsWithoutNorm(['Trắng', ' đen ', 'Hồng'], ['TRẮNG', 'Đen'])).toEqual(['Hồng']);
  });

  it('bỏ ô màu trống và màu lặp', () => {
    expect(colorsWithoutNorm(['', '  ', 'Hồng', 'hồng'], [])).toEqual(['Hồng']);
  });

  it('mã hàng chưa có định mức nào thì mọi màu đều thiếu', () => {
    expect(colorsWithoutNorm(['A', 'B'], [])).toEqual(['A', 'B']);
  });
});
