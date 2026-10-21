import { suggestPayType } from './hr';

describe('suggestPayType', () => {
  it('công nhân tổ may → khoán sản phẩm; công nhân tổ cắt và QA/QC → theo ngày công; còn lại → lương tháng', () => {
    expect(suggestPayType('Worker', 'Sewing')).toBe('PieceRate');
    expect(suggestPayType('Worker', 'Cutting')).toBe('Daily');
    expect(suggestPayType('QaQc', undefined)).toBe('Daily');
    expect(suggestPayType('Accountant', 'Office')).toBe('Monthly');
    expect(suggestPayType(undefined, 'Sewing')).toBeUndefined();
  });
});
