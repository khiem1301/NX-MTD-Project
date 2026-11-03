import { Result, Space, Spin, Tag } from 'antd';
import { useNavigate, useParams } from 'react-router-dom';
import { PageCard } from '@/components/common/PageCard';
import { QueryError } from '@/components/common/QueryError';
import { StatusTag } from '@/components/common/StatusTag';
import { ERROR_CODES } from '@/constants/errorCodes';
import { MESSAGES } from '@/constants/messages';
import { ORDER_STATUS_TAG } from '@/constants/orderStatus';
import { ROUTES } from '@/constants/routes';
import type { ApiError } from '@/types/api';
import { OrderDetailTabs } from '../components/OrderDetailTabs';
import { OrderSummary } from '../components/OrderSummary';
import { useOrderOverview } from '../hooks/useOrders';

/** UC09: Quản đốc xem đơn hàng, không sửa được thông tin đơn; NVL của đơn khai báo ở tab NVL. */
export function OrderOverviewPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { data, isLoading, error, refetch } = useOrderOverview(Number(id));

  if (isLoading) return <Spin />;
  if ((error as ApiError | null)?.errorCode === ERROR_CODES.CommonNotFound) {
    return <Result status="404" subTitle={MESSAGES.common.notFound} />;
  }
  if (error) return <QueryError error={error} onRetry={() => void refetch()} />;
  if (!data) return null;

  return (
    <PageCard
      onBack={() => navigate(ROUTES.managerOrders)}
      title={
        <Space size={8} wrap>
          {data.orderCode}
          <StatusTag status={data.status} labels={ORDER_STATUS_TAG} />
          {data.isDelayRisk && <Tag color="red">{MESSAGES.orders.delayRisk}</Tag>}
        </Space>
      }
      description={data.customerName}
    >
      <OrderSummary order={data} />
      <OrderDetailTabs orderId={data.orderId} status={data.status} lines={data.lines} />
    </PageCard>
  );
}
