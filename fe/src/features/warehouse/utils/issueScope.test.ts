import { allowedIssueItems } from './issueScope';

const none = new Set<number>();

describe('allowedIssueItems', () => {
  it('chưa chọn lệnh cắt, đơn hàng, đơn xin thì không chọn được gì', () => {
    expect([...allowedIssueItems({ requested: none })]).toEqual([]);
  });

  it('theo đơn hàng: chỉ NVL của đơn', () => {
    expect([...allowedIssueItems({ requested: none, orderItems: [1, 2] })]).toEqual([1, 2]);
  });

  it('theo lệnh cắt: chỉ vải của lệnh, không lấy NVL khác của đơn', () => {
    const allowed = allowedIssueItems({ requested: none, workOrderItems: [2], orderItems: [1, 2] });
    expect([...allowed]).toEqual([2]);
  });

  it('theo đơn xin: chỉ hàng trong đơn xin, vẫn trong phạm vi lệnh / đơn hàng', () => {
    const requested = new Set([2, 3]);
    expect([...allowedIssueItems({ requested, orderItems: [1, 2] })]).toEqual([2]);
    expect([...allowedIssueItems({ requested })]).toEqual([2, 3]);
  });
});
