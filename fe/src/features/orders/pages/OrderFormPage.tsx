import { Spin } from 'antd';
import { useParams } from 'react-router-dom';
import { QueryError } from '@/components/common/QueryError';
import { useOrder } from '../hooks/useOrders';
import { OrderForm } from '../components/OrderForm';

/** UC09: tạo / sửa đơn hàng nhiều màu × size. Trạng thái khác "Mới" thì dòng cũ bị khóa (D27). */
export function OrderFormPage() {
  const { id } = useParams();
  const orderId = id ? Number(id) : null;
  const order = useOrder(orderId);

  if (orderId !== null && order.isLoading) return <Spin />;
  if (orderId !== null && order.error)
    return <QueryError error={order.error} onRetry={() => void order.refetch()} />;
  // key theo rowVer: sau khi dữ liệu đổi (vd. tải lại do xung đột) form khởi tạo lại từ bản mới.
  return <OrderForm key={order.data?.rowVer ?? 'new'} orderId={orderId} detail={order.data} />;
}
