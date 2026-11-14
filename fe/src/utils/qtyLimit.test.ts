import type { RuleObject } from 'antd/es/form';
import { MESSAGES } from '@/constants/messages';
import { exceedsLimit, maxQtyRule, overLimitText } from './qtyLimit';

const validate = (rule: ReturnType<typeof maxQtyRule>, value: number | null) =>
  (rule as RuleObject).validator?.(rule as RuleObject, value, () => undefined);

describe('qtyLimit', () => {
  it('chỉ coi là vượt khi có trần và số lớn hơn trần', () => {
    expect(exceedsLimit(11, 10)).toBe(true);
    expect(exceedsLimit(10, 10)).toBe(false);
    expect(exceedsLimit(null, 10)).toBe(false);
    expect(exceedsLimit(11, undefined)).toBe(false);
  });

  it('câu lỗi mặc định ghi trần có dấu chấm ngăn nghìn', () => {
    expect(overLimitText(1250)).toBe('Không được vượt quá 1.250.');
  });

  it('rule báo lỗi khi vượt trần, bỏ qua khi trong trần', async () => {
    const rule = maxQtyRule(10);

    await expect(validate(rule, 10)).resolves.toBeUndefined();
    await expect(validate(rule, 11)).rejects.toThrow(overLimitText(10));
  });

  it('rule nhận mẫu câu riêng và chế độ chỉ cảnh báo', async () => {
    const rule = maxQtyRule(5, { message: 'Tối đa {0}', warningOnly: true });

    expect((rule as RuleObject).warningOnly).toBe(true);
    await expect(validate(rule, 6)).rejects.toThrow('Tối đa 5');
    expect(MESSAGES.common.overMaxQty).toContain('{0}');
  });
});
