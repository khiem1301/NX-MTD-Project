import { useState } from 'react';
import { PlusOutlined } from '@ant-design/icons';
import { Alert, Button, Card, Space, Typography } from 'antd';
import { DataTable } from '@/components/common/DataTable';
import { MESSAGES } from '@/constants/messages';
import { useSaveOrderMarkers } from '../hooks/useOrders';
import type { OrderMarkerProduct } from '../types';
import {
  newOrderMarkerRow,
  ORDER_MARKER_LIMITS,
  toOrderMarkerRows,
  toOrderMarkersPayload,
  validateOrderMarkers,
  type OrderMarkerRow,
} from '../utils/orderMarker';
import { orderMarkerColumns } from './orderMarkerColumns';

const text = MESSAGES.orders.markers;

type OrderMarkerProductCardProps = {
  orderId: number;
  product: OrderMarkerProduct;
  readOnly: boolean;
};

/** Bìa của một mã hàng trong đơn; lưu gửi đủ danh sách (bìa bỏ khỏi bảng bị xóa). Lưu xong dữ liệu tải lại (đổi key) nên dựng lại từ bản đã lưu. */
export function OrderMarkerProductCard({
  orderId,
  product,
  readOnly,
}: OrderMarkerProductCardProps) {
  const save = useSaveOrderMarkers(orderId);
  const [rows, setRows] = useState<OrderMarkerRow[]>(() => toOrderMarkerRows(product));
  const [error, setError] = useState('');
  const noGroups = product.markerGroups.length === 0;
  const editable = !readOnly && !noGroups;

  const handleSave = () => {
    const problem = validateOrderMarkers(rows, product.sizes);
    setError(problem);
    if (problem) return;
    save.mutate({
      productId: product.productId,
      body: toOrderMarkersPayload(rows, product.sizes),
    });
  };

  return (
    <Card
      size="small"
      title={`${product.productCode} - ${product.productName}`}
      extra={
        editable && (
          <Button type="primary" size="small" loading={save.isPending} onClick={handleSave}>
            {MESSAGES.common.save}
          </Button>
        )
      }
    >
      {noGroups && <Typography.Text type="secondary">{text.noGroups}</Typography.Text>}
      {error && <Alert type="warning" showIcon message={error} style={{ marginBottom: 8 }} />}
      {!noGroups && (
        <DataTable<OrderMarkerRow>
          rowKey="key"
          size="small"
          pagination={false}
          scroll={{ x: 'max-content' }}
          locale={{ emptyText: text.noMarkers }}
          dataSource={rows}
          columns={orderMarkerColumns({
            sizes: product.sizes,
            groups: product.markerGroups,
            readOnly: !editable,
            setRows,
          })}
        />
      )}
      {editable && rows.length < ORDER_MARKER_LIMITS.maxMarkers && (
        <Space style={{ marginTop: 8 }}>
          <Button
            type="dashed"
            size="small"
            icon={<PlusOutlined />}
            onClick={() => setRows((r) => [...r, newOrderMarkerRow(product)])}
          >
            {text.addMarker}
          </Button>
        </Space>
      )}
    </Card>
  );
}
