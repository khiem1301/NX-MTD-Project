import { MESSAGES } from '@/constants/messages';
import { fromPiece, lineCap } from './requisitionItems';

const item = (availableQty: number) => ({
  id: 1,
  label: 'V1 Kate',
  unit: 'm',
  materialType: 'FABRIC',
  availableQty,
});

describe('lineCap', () => {
  it('chưa chọn hàng thì không có trần', () => {
    expect(lineCap(undefined, 10)).toBeUndefined();
  });

  it('trần là số khả dụng trong kho', () => {
    expect(lineCap(item(40), undefined)).toEqual({ cap: 40, message: MESSAGES.requests.overStock });
  });

  it('số còn xin được của lệnh cắt nhỏ hơn khả dụng thì lấy số của lệnh', () => {
    expect(lineCap(item(40), 25)).toEqual({ cap: 25, message: MESSAGES.requests.overRemaining });
  });

  it('khả dụng âm tính như 0', () => {
    expect(lineCap(item(-3), undefined)?.cap).toBe(0);
  });
});

describe('fromPiece', () => {
  it('nhãn là mã hàng màu/size', () => {
    expect(
      fromPiece({
        orderLineId: 11,
        productCode: 'AO1',
        productName: 'Áo',
        color: 'Đỏ',
        sizeCode: 'M',
        orderedQty: 100,
        availableQty: 30,
      }),
    ).toMatchObject({ id: 11, label: 'AO1 Đỏ/M', availableQty: 30 });
  });
});
