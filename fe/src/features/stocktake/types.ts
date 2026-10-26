import type { PageParams } from '@/types/api';

export type StocktakeListItem = {
  stocktakeId: number;
  stocktakeCode: string;
  warehouseType: string;
  scope: string | null;
  stocktakeDate: string;
  status: string;
  createdAt: string;
  approvedAt: string | null;
  lineCount: number;
  differenceLineCount: number;
};

export type StocktakeLine = {
  stocktakeLineId: number;
  materialId: number | null;
  materialCode: string | null;
  materialName: string | null;
  unit: string | null;
  materialColor: string | null;
  orderLineId: number | null;
  productComponentMaterialId: number | null;
  componentName: string | null;
  orderCode: string | null;
  productCode: string | null;
  color: string | null;
  sizeCode: string | null;
  bookQty: number;
  countedQty: number | null;
  differenceQty: number;
  note: string | null;
};

export type Stocktake = Omit<StocktakeListItem, 'lineCount' | 'differenceLineCount'> & {
  returnReason: string | null;
  lines: StocktakeLine[];
};

export type StocktakeQuery = PageParams & { status?: string; warehouseType?: string };

export type SaveStocktakePayload = {
  warehouseType: string;
  scope?: string;
  stocktakeDate: string;
  lines: {
    materialId?: number;
    materialColor?: string;
    orderLineId?: number;
    productComponentMaterialId?: number | null;
    countedQty?: number | null;
    note?: string;
  }[];
};
