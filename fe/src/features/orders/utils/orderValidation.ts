import { z } from 'zod';
import { MESSAGES } from '@/constants/messages';
import { rowTotal, type ProductBlock } from './orderGrid';

const text = MESSAGES.orders.validation;

/** Giới hạn khớp OrderDtoValidators ở BE; FE chỉ báo sớm, BE vẫn kiểm lại. */
export const MAX_ORDER_QTY = 1_000_000;
export const MAX_CMT_UNIT_PRICE = 100_000_000;
export const MAX_COLOR_LENGTH = 50;

export const orderCodeSchema = z
  .string()
  .trim()
  .min(1, MESSAGES.common.required)
  .max(30, text.codeMax)
  .regex(/^[A-Za-z0-9._-]+$/, text.codeFormat);

export const orderNoteSchema = z.string().max(1000, text.noteMax);

const hasAtMostTwoDecimals = (value: number) => Math.round(value * 100) / 100 === value;

const cmtPriceSchema = z
  .number({ error: text.priceRequired })
  .min(0, text.priceMin)
  .max(MAX_CMT_UNIT_PRICE, text.priceMax)
  .refine(hasAtMostTwoDecimals, text.priceDecimals);

const qtySchema = z
  .number()
  .int(text.qtyInvalid)
  .min(0, text.qtyInvalid)
  .max(MAX_ORDER_QTY, text.qtyMax);

const firstError = (schema: z.ZodType, value: unknown): string | undefined =>
  schema.safeParse(value).error?.issues[0]?.message;

/** Lỗi của một khối mã hàng, tra theo `product`, `price`, `lines`, `colorErrorKey`, `qtyErrorKey`. */
export type BlockErrors = Record<string, string>;

export const colorErrorKey = (rowKey: string) => `color:${rowKey}`;
export const qtyErrorKey = (rowKey: string, size: string) => `qty:${rowKey}:${size}`;

const hasContent = (block: ProductBlock) =>
  block.productId !== null ||
  block.cmtUnitPrice !== null ||
  block.rows.some((r) => r.color.trim() !== '' || rowTotal(block, r) > 0);

const checkRows = (block: ProductBlock, locked: boolean, errors: BlockErrors) => {
  const seenColors = new Set<string>();
  for (const row of block.rows) {
    const color = row.color.trim().toLowerCase();
    const hasQty = rowTotal(block, row) > 0;
    if (color.length > MAX_COLOR_LENGTH) errors[colorErrorKey(row.key)] = text.colorMax;
    else if (!color && hasQty) errors[colorErrorKey(row.key)] = text.colorRequired;
    else if (color && !hasQty) errors[colorErrorKey(row.key)] = text.colorNoQty;
    else if (color && seenColors.has(color)) errors[colorErrorKey(row.key)] = text.colorDuplicate;
    seenColors.add(color);

    for (const size of block.sizes) {
      const cell = row.cells[size];
      if (locked && cell && cell.orderLineId !== null && !cell.qty)
        errors[qtyErrorKey(row.key, size)] = text.oldLineEmpty;
      else if (cell?.qty) {
        const error = firstError(qtySchema, cell.qty);
        if (error) errors[qtyErrorKey(row.key, size)] = error;
      }
    }
  }
};

/** Khối trống hoàn toàn được bỏ qua; khối đã nhập dở phải đủ mã hàng, đơn giá và số lượng, không âm thầm bỏ dữ liệu. */
const validateBlock = (block: ProductBlock, others: ProductBlock[], locked: boolean) => {
  const errors: BlockErrors = {};
  if (!hasContent(block)) return errors;
  if (block.productId === null) errors.product = text.productRequired;
  else if (others.some((b) => b.productId === block.productId))
    errors.product = MESSAGES.orders.duplicateProduct;
  const priceError = firstError(cmtPriceSchema, block.cmtUnitPrice);
  if (priceError) errors.price = priceError;
  if (!block.rows.some((r) => r.color.trim() !== '' || rowTotal(block, r) > 0))
    errors.lines = text.blockEmpty;
  checkRows(block, locked, errors);
  return errors;
};

/** Lỗi theo khóa của từng khối; khối không lỗi không có trong kết quả. */
export const validateBlocks = (
  blocks: ProductBlock[],
  locked: boolean,
): Record<string, BlockErrors> =>
  Object.fromEntries(
    blocks.flatMap((block) => {
      const others = blocks.filter((b) => b.key !== block.key);
      const errors = validateBlock(block, others, locked);
      return Object.keys(errors).length > 0 ? [[block.key, errors]] : [];
    }),
  );
