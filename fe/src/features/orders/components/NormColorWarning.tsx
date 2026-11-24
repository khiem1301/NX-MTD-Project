import { Alert } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { fill } from '@/utils/format';
import { useProductSpec } from '../hooks/useOrders';
import type { ProductBlock } from '../utils/orderGrid';
import { colorsWithoutNorm } from '../utils/orderNorms';

type NormColorWarningProps = {
  block: ProductBlock;
  /** Nhãn mã hàng (để nêu trong thông báo). */
  productLabel: string | undefined;
  /** Mã hàng khai trên form (chưa ghi, Id âm): các màu đã có định mức trong file Excel đã chọn; mã có sẵn thì bỏ trống. */
  stagedNormColors?: readonly string[];
};

/**
 * Cảnh báo ở khối mã hàng của form đơn: màu của đơn chưa có định mức NVL ở mã hàng (màu mới thêm hoặc mã hàng chưa khai định mức).
 * Không chặn Lưu (có thể nhập định mức sau), nhưng đơn thiếu số NVL cần cho màu đó nên phải nhắc ngay.
 */
export function NormColorWarning({ block, productLabel, stagedNormColors }: NormColorWarningProps) {
  const staged = block.productId !== null && block.productId < 0;
  const spec = useProductSpec(block.productId !== null && !staged ? block.productId : undefined);
  if (block.productId === null) return null;
  // Mã có sẵn: chờ đọc xong định mức; không đọc được thì không cảnh báo sai.
  if (!staged && !spec.data) return null;
  const known = staged ? (stagedNormColors ?? []) : spec.data!.norms.map((n) => n.color);
  const missing = colorsWithoutNorm(
    block.rows.map((r) => r.color),
    known,
  );
  if (missing.length === 0) return null;
  return (
    <Alert
      type="warning"
      showIcon
      style={{ marginBottom: 12 }}
      message={fill(MESSAGES.orders.colorWithoutNorm, missing.join(', '), productLabel ?? '')}
    />
  );
}
