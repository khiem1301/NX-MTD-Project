import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { message } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { warehouseApi } from '@/features/warehouse/api/warehouseApi';
import { orderMarkerApi, productApi, type ReviewScope } from '../api/orderApi';
import type { ReviewTable } from '../utils/excelReview';
import type { ExistingData } from '../utils/excelReviewExisting';

type Options = {
  productId: number | null;
  orderId: number | null;
  scope: ReviewScope;
  /** Các bảng đang hiện ở popup. */
  tables: readonly ReviewTable[];
};

/** Bảng nào nạp được từ hệ thống: định mức mã hàng cần có mã hàng; bìa và NVL của đơn cần có đơn. */
export const loadableTables = ({
  productId,
  orderId,
}: Pick<Options, 'productId' | 'orderId'>): ReviewTable[] => [
  ...(productId !== null ? (['components', 'norms', 'groups'] as const) : []),
  ...(productId !== null && orderId !== null ? (['markers'] as const) : []),
  ...(orderId !== null ? (['orderMaterials'] as const) : []),
];

const specOf = (productId: number, scope: ReviewScope) =>
  scope === 'admin' ? productApi.getSpec(productId) : orderMarkerApi.productSpec(productId);

/** Chi tiết và màu đang có ở mã hàng, để chọn trong ô của bảng sửa (không phải gõ lại). Quản đốc đọc qua API riêng. */
export type ReviewDbOptions = {
  components: { name: string; panel: boolean }[];
  colors: string[];
};

export function useReviewDbOptions(productId: number | null, scope: ReviewScope): ReviewDbOptions {
  const spec = useQuery({
    queryKey: ['review-db-options', productId],
    queryFn: () => specOf(productId ?? 0, scope),
    enabled: productId !== null,
    staleTime: 0,
  });
  const data = spec.data;
  return {
    components: (data?.components ?? [])
      .filter((c) => c.status === 'Active')
      .map((c) => ({ name: c.componentName, panel: c.componentType === 'Panel' })),
    colors: [
      ...(data?.variants ?? []).filter((v) => v.status === 'Active').map((v) => v.color),
      ...(data?.markerFabrics ?? []).map((f) => f.color),
      ...(data?.norms ?? []).map((n) => n.color),
    ],
  };
}

/**
 * Đọc dữ liệu đang có trong hệ thống (cho các bảng của popup) để nạp vào bảng sửa. Không ghi gì.
 * Lỗi đọc báo bằng thông báo, không chặn popup.
 */
export function useExistingData({ productId, orderId, scope, tables }: Options) {
  const [loading, setLoading] = useState(false);
  const wanted = loadableTables({ productId, orderId }).filter((t) => tables.includes(t));

  const load = async (): Promise<ExistingData | null> => {
    setLoading(true);
    try {
      const data: ExistingData = {};
      if (productId !== null && wanted.some((t) => ['components', 'norms', 'groups'].includes(t)))
        data.spec = await specOf(productId, scope);
      if (productId !== null && orderId !== null && wanted.includes('markers')) {
        const all = await orderMarkerApi.get(orderId);
        data.markers = all.products.find((p) => p.productId === productId);
      }
      if (orderId !== null && wanted.includes('orderMaterials'))
        data.orderMaterials = await warehouseApi.getOrderMaterials(scope, orderId);
      return data;
    } catch (error) {
      void message.error(error instanceof Error ? error.message : MESSAGES.common.unknownError);
      return null;
    } finally {
      setLoading(false);
    }
  };

  return { load, loading, available: wanted.length > 0 };
}
