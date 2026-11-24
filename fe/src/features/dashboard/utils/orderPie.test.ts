import { PIE_COLOR, orderPieSlices, pieGradient } from './orderPie';

const progress = {
  orderedQty: 100,
  cutQty: 80,
  issuedToSewingQty: 60,
  finishedQty: 40,
  finishedReceivedQty: 10,
};

describe('orderPieSlices', () => {
  it('chia số đặt thành các chặng loại trừ nhau, tổng bằng số đặt', () => {
    const slices = orderPieSlices(progress);
    const qty = Object.fromEntries(slices.map((s) => [s.stage, s.qty]));

    expect(qty).toEqual({ cut: 20, sew: 20, qc: 30, ship: 10, rest: 20 });
    expect(slices.reduce((sum, s) => sum + s.qty, 0)).toBe(100);
  });

  it('số liệu lệch (hoàn thiện nhiều hơn cấp may) không ra số âm hay vượt số đặt', () => {
    const slices = orderPieSlices({ ...progress, issuedToSewingQty: 10, finishedQty: 90 });

    expect(slices.every((s) => s.qty >= 0)).toBe(true);
    expect(slices.reduce((sum, s) => sum + s.qty, 0)).toBe(100);
  });

  it('đơn chưa có số lượng: mọi chặng bằng 0', () => {
    expect(orderPieSlices({ ...progress, orderedQty: 0 }).every((s) => s.qty === 0)).toBe(true);
  });
});

describe('pieGradient', () => {
  it('ghép các chặng có hàng thành conic-gradient theo tỉ lệ', () => {
    const css = pieGradient(
      orderPieSlices({ ...progress, orderedQty: 100, cutQty: 50, issuedToSewingQty: 0 }),
    );

    expect(css).toBe(`conic-gradient(${PIE_COLOR.cut} 0% 50%, ${PIE_COLOR.rest} 50% 100%)`);
  });

  it('không có số lượng thì xám trơn', () => {
    expect(pieGradient(orderPieSlices({ ...progress, orderedQty: 0 }))).toBe(PIE_COLOR.rest);
  });
});
