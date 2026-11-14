import { ISSUE_LIMITS, materialIssueRule, PIECE_ISSUE_RULE, roundIssueQty } from './issueQuantity';

describe('issueQuantity', () => {
  it('vải cho 2 chữ số lẻ, NVL khác là số nguyên', () => {
    expect(materialIssueRule('FABRIC')).toEqual({ decimals: 2, min: 0.01, max: 9_999_999 });
    expect(materialIssueRule('TRIM')).toEqual({ decimals: 0, min: 1, max: 9_999_999 });
    expect(materialIssueRule(undefined).decimals).toBe(0);
  });

  it('BTP là số nguyên từ 1 đến 1.000.000', () => {
    expect(PIECE_ISSUE_RULE).toEqual({ decimals: 0, min: 1, max: ISSUE_LIMITS.maxPieceQty });
  });

  it('đổi sang NVL không lẻ thì làm tròn, tối thiểu 1', () => {
    expect(roundIssueQty(2.5, 'TRIM')).toBe(3);
    expect(roundIssueQty(0.2, 'TRIM')).toBe(1);
    expect(roundIssueQty(2.55, 'FABRIC')).toBe(2.55);
  });
});
