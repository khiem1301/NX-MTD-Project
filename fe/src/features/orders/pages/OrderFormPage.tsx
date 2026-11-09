import { Spin } from 'antd';
import { useParams, useSearchParams } from 'react-router-dom';
import { QueryError } from '@/components/common/QueryError';
import { useOrder } from '../hooks/useOrders';
import { OrderForm } from '../components/OrderForm';

/** UC09: tạo / sửa đơn hàng nhiều màu × size. Trạng thái khác "Mới" thì dòng cũ bị khóa (D27).
 *  `?copyFrom=` trên màn tạo: nạp khách + dòng hàng từ đơn nguồn, mã đơn lấy mới. */
export function OrderFormPage() {
  const { id } = useParams();
  const [searchParams] = useSearchParams();
  const orderId = id ? Number(id) : null;
  const rawCopyFrom = orderId === null ? Number(searchParams.get('copyFrom')) : NaN;
  const copyFromId = Number.isInteger(rawCopyFrom) && rawCopyFrom > 0 ? rawCopyFrom : null;
  const loadId = orderId ?? copyFromId;
  const order = useOrder(loadId);

  if (loadId !== null && order.isLoading) return <Spin />;
  if (loadId !== null && order.error)
    return <QueryError error={order.error} onRetry={() => void order.refetch()} />;

  // key theo rowVer / copyFrom: sau khi dữ liệu đổi (vd. tải lại do xung đột) form khởi tạo lại từ bản mới.
  const formKey =
    orderId !== null
      ? (order.data?.rowVer ?? 'edit')
      : copyFromId !== null
        ? `copy-${copyFromId}-${order.data?.rowVer ?? ''}`
        : 'new';

  return (
    <OrderForm
      key={formKey}
      orderId={orderId}
      detail={orderId !== null ? order.data : undefined}
      copyFrom={orderId === null && copyFromId !== null ? order.data : undefined}
    />
  );
}
