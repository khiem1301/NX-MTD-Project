import { createContext, useContext } from 'react';
import type { MaterialExcelRow } from '../types';

const NONE: MaterialExcelRow[] = [];

/**
 * DEV-ALL-193: NVL mới đã khai ở "NVL của đơn" trên form đơn (chưa ghi, tạo khi Lưu đơn). Form đơn chỉ bọc phần tạo mã hàng, nên
 * popup kiểm tra Excel của mã hàng coi các NVL này như đã có; popup NVL của đơn và màn Mã hàng không nhận (rỗng).
 */
export const PendingMaterialsContext = createContext<MaterialExcelRow[]>(NONE);

export const usePendingMaterials = () => useContext(PendingMaterialsContext);
