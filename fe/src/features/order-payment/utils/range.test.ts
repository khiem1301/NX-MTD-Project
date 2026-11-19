import { allowedRange } from './range';

describe('allowedRange', () => {
  it('giá trị chốt dương: nhập được từ -đã trả đến phần còn lại', () => {
    expect(allowedRange(1_000, 400)).toEqual({ min: -400, max: 600 });
  });

  it('giá trị chốt âm (mình đền): nhập được từ phần còn lại phải trả đến phần đã trả', () => {
    expect(allowedRange(-1_000, -300)).toEqual({ min: -700, max: 300 });
  });

  it('chưa trả gì: khoảng bằng đúng giá trị chốt', () => {
    expect(allowedRange(1_000, 0)).toEqual({ min: 0, max: 1_000 });
    expect(allowedRange(-1_000, 0)).toEqual({ min: -1_000, max: 0 });
  });
});
