import { filterColors, foldVietnamese } from './colorSearch';

describe('colorSearch', () => {
  it('bỏ dấu và đổi đ thành d', () => {
    expect(foldVietnamese('Đen')).toBe('den');
    expect(foldVietnamese(' Xanh Dương ')).toBe('xanh duong');
  });

  it('gõ "đ" hoặc "d" đều ra Đen đầu tiên', () => {
    const colors = ['Be', 'Đen', 'Xanh', 'Xanh Đen', 'Trắng'];
    expect(filterColors(colors, 'đ')[0]).toBe('Đen');
    expect(filterColors(colors, 'd')).toEqual(['Đen', 'Xanh Đen']);
  });

  it('khớp theo đầu từ, trả hết khi bỏ trống', () => {
    const colors = ['Be', 'Đen', 'Xanh Đen'];
    expect(filterColors(colors, 'xa')).toEqual(['Xanh Đen']);
    expect(filterColors(colors, '')).toEqual(colors);
    expect(filterColors(colors, 'zzz')).toEqual([]);
  });
});
