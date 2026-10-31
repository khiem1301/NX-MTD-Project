import { SIZE_ORDER } from '@/constants/orderStatus';
import type { OrderLine, OrderLineInput } from '../types';

export type GridCell = { orderLineId: number | null; qty: number | null };
export type GridRow = { key: string; color: string; cells: Record<string, GridCell> };
export type ProductBlock = {
  key: string;
  productId: number | null;
  cmtUnitPrice: number | null;
  sizes: string[];
  rows: GridRow[];
};

export const DEFAULT_SIZES = ['S', 'M', 'L', 'XL'];

export const sortSizes = (sizes: string[]): string[] => {
  const rank = (s: string) => {
    const i = (SIZE_ORDER as readonly string[]).indexOf(s);
    return i === -1 ? SIZE_ORDER.length : i;
  };
  return [...new Set(sizes)].sort((a, b) => rank(a) - rank(b) || a.localeCompare(b));
};

/** Gom dòng theo (mã hàng, đơn giá): cùng mã hàng nhưng khác giá thành 2 khối, không mất dữ liệu. */
export const fromLines = (lines: OrderLine[]): ProductBlock[] => {
  const blocks = new Map<string, ProductBlock>();
  for (const l of lines) {
    const blockKey = `${l.productId}|${l.cmtUnitPrice}`;
    let block = blocks.get(blockKey);
    if (!block) {
      block = {
        key: blockKey,
        productId: l.productId,
        cmtUnitPrice: l.cmtUnitPrice,
        sizes: [],
        rows: [],
      };
      blocks.set(blockKey, block);
    }
    block.sizes.push(l.sizeCode);
    let row = block.rows.find((r) => r.color.toLowerCase() === l.color.toLowerCase());
    if (!row) {
      row = { key: `${blockKey}|${l.color}`, color: l.color, cells: {} };
      block.rows.push(row);
    }
    row.cells[l.sizeCode] = { orderLineId: l.orderLineId, qty: l.orderedQty };
  }
  return [...blocks.values()].map((b) => ({ ...b, sizes: sortSizes(b.sizes) }));
};

/** Ô trống / 0 không tạo dòng. Dòng cũ có orderLineId giữ nguyên Id để BE biết là sửa. */
export const toLines = (blocks: ProductBlock[]): OrderLineInput[] =>
  blocks.flatMap((b) => {
    const productId = b.productId;
    if (productId === null) return [];
    return b.rows.flatMap((r) =>
      b.sizes.flatMap((size) => {
        const cell = r.cells[size];
        if (!cell || cell.qty === null || cell.qty <= 0) return [];
        return [
          {
            orderLineId: cell.orderLineId,
            productId,
            color: r.color.trim(),
            sizeCode: size,
            orderedQty: cell.qty,
            cmtUnitPrice: b.cmtUnitPrice ?? 0,
          },
        ];
      }),
    );
  });

export const blockTotal = (b: ProductBlock): number =>
  b.rows.reduce((sum, r) => sum + b.sizes.reduce((s, size) => s + (r.cells[size]?.qty ?? 0), 0), 0);

export const blocksTotals = (blocks: ProductBlock[]): { qty: number; amount: number } =>
  blocks.reduce(
    (acc, b) => {
      const qty = blockTotal(b);
      return { qty: acc.qty + qty, amount: acc.amount + qty * (b.cmtUnitPrice ?? 0) };
    },
    { qty: 0, amount: 0 },
  );

let blockSeq = 0;
export const newBlock = (): ProductBlock => ({
  key: `new-${(blockSeq += 1)}`,
  productId: null,
  cmtUnitPrice: null,
  sizes: [...DEFAULT_SIZES],
  rows: [{ key: `new-row-${blockSeq}`, color: '', cells: {} }],
});
