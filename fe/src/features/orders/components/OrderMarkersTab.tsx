import { useState } from 'react';
import { Button, Empty, Flex, Skeleton, Typography } from 'antd';
import { UploadOutlined } from '@ant-design/icons';
import { MESSAGES } from '@/constants/messages';
import { useOrderMarkers } from '../hooks/useOrders';
import { markerVersion } from '../utils/orderMarker';
import { OrderMarkerProductCard } from './OrderMarkerProductCard';
import { SpecImportModal } from './SpecImportModal';

const text = MESSAGES.orders.markers;

type OrderMarkersTabProps = {
  orderId: number;
  orderCode: string;
  /** Đơn đã đóng: chỉ xem (BE cũng chặn lưu). */
  isClosed: boolean;
};

/**
 * Tab "Bìa cắt" của chi tiết đơn (DEV-ALL-85): Admin, Quản đốc khai bìa theo từng mã hàng để lệnh cắt ước số mét vải.
 * DEV-ALL-114: nút Nhập từ Excel nhập định mức mã hàng và bìa của đơn trong một file.
 */
export function OrderMarkersTab({ orderId, orderCode, isClosed }: OrderMarkersTabProps) {
  const markers = useOrderMarkers(orderId);
  const [importing, setImporting] = useState(false);
  // Khung chờ giữ chỗ gần bằng bảng thật để lúc tải xong trang không nhảy chiều cao.
  if (markers.isLoading) return <Skeleton active paragraph={{ rows: 4 }} />;
  const products = markers.data?.products ?? [];

  return (
    <>
      <Flex justify="space-between" align="flex-start" gap={12} wrap>
        <Typography.Paragraph type="secondary" style={{ flex: 1, minWidth: 280 }}>
          {text.hint}
        </Typography.Paragraph>
        {!isClosed && products.length > 0 && (
          <Button icon={<UploadOutlined />} onClick={() => setImporting(true)}>
            {MESSAGES.orders.specImport.button}
          </Button>
        )}
      </Flex>
      {products.length === 0 && (
        <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={text.empty} />
      )}
      <Flex vertical gap={12}>
        {products.map((p) => (
          <OrderMarkerProductCard
            key={markerVersion(p)}
            orderId={orderId}
            product={p}
            readOnly={isClosed}
          />
        ))}
      </Flex>
      {importing && (
        <SpecImportModal
          source={{
            scope: 'manager',
            orderId,
            orderCode,
            products: products.map((p) => ({
              productId: p.productId,
              productCode: p.productCode,
              productName: p.productName,
            })),
          }}
          onClose={() => setImporting(false)}
        />
      )}
    </>
  );
}
