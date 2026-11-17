import type { OrderImportRow } from '../types';
import { sortSizes, type GridCell, type GridRow, type ProductBlock } from './orderGrid';

let importSeq = 0;
const nextKey = (prefix: string) => `${prefix}-${(importSeq += 1)}`;

/** Khối mới chưa chọn mã hàng và chưa gõ gì: bỏ đi khi nhập để form không còn khối trống thừa. */
const isBlankBlock = (b: ProductBlock) =>
  b.productId === null &&
  b.rows.every((r) => r.color.trim() === '' && Object.values(r.cells).every((c) => !c.qty));

const sameColor = (a: string, b: string) => a.trim().toLowerCase() === b.trim().toLowerCase();

/** Ghi số lượng của dòng file vào dòng màu (giữ orderLineId của ô đã có để BE biết là sửa). */
const applyRow = (row: GridRow, source: OrderImportRow): GridRow => {
  const cells: Record<string, GridCell> = { ...row.cells };
  for (const q of source.quantities) {
    cells[q.sizeCode] = { ...(cells[q.sizeCode] ?? { orderLineId: null }), qty: q.qty };
  }
  return { ...row, cells };
};

const mergeIntoBlock = (block: ProductBlock, rows: OrderImportRow[]): ProductBlock => {
  let gridRows = [...block.rows];
  for (const source of rows) {
    const index = gridRows.findIndex((r) => sameColor(r.color, source.color));
    if (index >= 0) gridRows[index] = applyRow(gridRows[index]!, source);
    else
      gridRows = [
        ...gridRows,
        applyRow({ key: nextKey('import-row'), color: source.color, cells: {} }, source),
      ];
  }
  const sizes = sortSizes([
    ...block.sizes,
    ...rows.flatMap((r) => r.quantities.map((q) => q.sizeCode)),
  ]);
  return {
    ...block,
    cmtUnitPrice: rows[0]?.cmtUnitPrice ?? block.cmtUnitPrice,
    sizes,
    rows: gridRows,
  };
};

/**
 * DEV-ALL-117: gộp các dòng hợp lệ của file vào form đơn. Mã hàng đã có trên form: dòng cùng màu được ghi số lượng các size trong
 * file (size khác giữ nguyên), màu mới thêm dòng, đơn giá CMT lấy theo file. Mã hàng chưa có: thêm khối mới. Trả kèm nhãn mã hàng.
 */
export const mergeOrderImport = (
  blocks: ProductBlock[],
  rows: OrderImportRow[],
): { blocks: ProductBlock[]; labels: Record<number, string> } => {
  const valid = rows.filter((r) => r.error === null && r.productId !== null);
  const byProduct = new Map<number, OrderImportRow[]>();
  for (const r of valid) byProduct.set(r.productId!, [...(byProduct.get(r.productId!) ?? []), r]);

  let next = blocks.filter((b) => !isBlankBlock(b));
  const labels: Record<number, string> = {};
  for (const [productId, productRows] of byProduct) {
    const first = productRows[0]!;
    labels[productId] = `${first.productCode} · ${first.productName ?? ''}`;
    const index = next.findIndex((b) => b.productId === productId);
    if (index >= 0) {
      next = next.map((b, i) => (i === index ? mergeIntoBlock(b, productRows) : b));
    } else {
      const empty: ProductBlock = {
        key: nextKey('import-block'),
        productId,
        cmtUnitPrice: null,
        sizes: [],
        rows: [],
      };
      next = [...next, mergeIntoBlock(empty, productRows)];
    }
  }
  return { blocks: next, labels };
};
