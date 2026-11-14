import type { Rule } from 'antd/es/form';
import { MESSAGES } from '@/constants/messages';
import { fill, formatQty } from '@/utils/format';

/**
 * Trần nghiệp vụ của ô số lượng (số còn lại, số đã giao, khả dụng…), khác trần cố định `max` của QtyInput:
 * gõ vượt trần nghiệp vụ thì ô không tự cắt số mà báo lỗi để người dùng thấy mình nhập sai.
 */
export const exceedsLimit = (value: number | null | undefined, limit: number | undefined) =>
  limit !== undefined && value !== null && value !== undefined && value > limit;

/** Câu lỗi mặc định khi vượt trần nghiệp vụ: "Không được vượt quá 1.250.". */
export const overLimitText = (limit: number) => fill(MESSAGES.common.overMaxQty, formatQty(limit));

type MaxQtyRuleOptions = {
  /** Mẫu câu lỗi có {0} là trần; mặc định MESSAGES.common.overMaxQty. */
  message?: string;
  /** true: chỉ cảnh báo (chữ vàng), form vẫn lưu được. Mặc định chặn lưu. */
  warningOnly?: boolean;
};

/** Rule của Form.Item: số nhập vượt `limit` thì báo ngay dưới ô (mặc định chặn lưu). */
export const maxQtyRule = (limit: number | undefined, options: MaxQtyRuleOptions = {}): Rule => ({
  warningOnly: options.warningOnly,
  validator: async (_rule, value?: number | null) => {
    if (limit !== undefined && exceedsLimit(value, limit))
      throw new Error(fill(options.message ?? MESSAGES.common.overMaxQty, formatQty(limit)));
  },
});
