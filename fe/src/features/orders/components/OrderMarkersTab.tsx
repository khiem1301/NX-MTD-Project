import { Empty, Flex, Spin, Typography } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { useOrderMarkers } from '../hooks/useOrders';
import { OrderMarkerProductCard } from './OrderMarkerProductCard';

const text = MESSAGES.orders.markers;

type OrderMarkersTabProps = {
  orderId: number;
  /** Đơn đã đóng: chỉ xem (BE cũng chặn lưu). */
  isClosed: boolean;
};

/** Tab "Bìa cắt" của chi tiết đơn (DEV-ALL-85): Admin, Quản đốc khai bìa theo từng mã hàng để lệnh cắt ước số mét vải. */
export function OrderMarkersTab({ orderId, isClosed }: OrderMarkersTabProps) {
  const markers = useOrderMarkers(orderId);
  if (markers.isLoading) return <Spin />;
  const products = markers.data?.products ?? [];

  return (
    <>
      <Typography.Paragraph type="secondary">{text.hint}</Typography.Paragraph>
      {products.length === 0 && (
        <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={text.empty} />
      )}
      <Flex vertical gap={12}>
        {products.map((p) => (
          <OrderMarkerProductCard
            key={`${p.productId}-${markers.dataUpdatedAt}`}
            orderId={orderId}
            product={p}
            readOnly={isClosed}
          />
        ))}
      </Flex>
    </>
  );
}
