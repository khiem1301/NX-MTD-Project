import { useState } from 'react';
import {
  Alert,
  Card,
  Checkbox,
  Progress,
  Space,
  Spin,
  Tag,
  Tooltip,
  Typography,
  type TableColumnsType,
} from 'antd';
import { useQuery } from '@tanstack/react-query';
import { QueryError } from '@/components/common/QueryError';
import { StatusTag } from '@/components/common/StatusTag';
import { ORDER_STATUS_TAG } from '@/constants/orderStatus';
import { useRolePath } from '@/hooks/useRolePath';
import { apiClient } from '@/services/apiClient';
import type { ApiResponse } from '@/types/api';
import { formatDate } from '@/utils/format';
import { DetailModal } from '@/components/common/DetailModal';
import { DetailLink } from '@/components/common/DetailLink';
import { DataTable } from '@/components/common/DataTable';

type OrderProgress = {
  orderId: number;
  orderCode: string;
  customerName: string;
  dueDate: string;
  daysLeft: number;
  status: string;
  materialStatus: string;
  isDelayRisk: boolean;
  delayRiskSince: string | null;
  orderedQty: number;
  cutQty: number;
  cutHandedToWarehouseQty: number;
  issuedToSewingQty: number;
  finishedQty: number;
  finishedReceivedQty: number;
  deliveredQty: number;
  reworkQty: number;
  scrapQty: number;
  defectRatePct: number;
};

type LineProgress = {
  orderLineId: number;
  productCode: string;
  color: string;
  sizeCode: string;
  orderedQty: number;
  cutQty: number;
  cutHandedToWarehouseQty: number;
  issuedToSewingQty: number;
  finishedQty: number;
  finishedReceivedQty: number;
  deliveredQty: number;
  reworkQty: number;
  scrapQty: number;
  defectRatePct: number | null;
};

const get = async <T,>(url: string, params?: object): Promise<T> =>
  (await apiClient.get<ApiResponse<T>>(url, { params })).data.data;

const pct = (value: number, total: number) =>
  total > 0 ? Math.min(100, Math.round((100 * value) / total)) : 0;

function Stage({ value, total, label }: { value: number; total: number; label: string }) {
  return (
    <Tooltip title={`${label}: ${value} / ${total}`}>
      <Progress
        percent={pct(value, total)}
        size="small"
        format={() => value}
        style={{ minWidth: 90, margin: 0 }}
      />
    </Tooltip>
  );
}

function LinesDrawer({ order, onClose }: { order: OrderProgress | null; onClose: () => void }) {
  const role = useRolePath();
  const lines = useQuery({
    queryKey: ['dashboard', 'lines', role, order?.orderId],
    queryFn: () => get<LineProgress[]>(`/api/v1/${role}/order-progress/${order?.orderId}/lines`),
    enabled: order !== null,
  });
  const columns: TableColumnsType<LineProgress> = [
    {
      title: 'Mã hàng - màu - size',
      key: 'line',
      render: (_, l) => `${l.productCode} - ${l.color} - ${l.sizeCode}`,
    },
    { title: 'Đặt', dataIndex: 'orderedQty', align: 'right' },
    { title: 'Cắt', dataIndex: 'cutQty', align: 'right' },
    { title: 'Vào kho BTP', dataIndex: 'cutHandedToWarehouseQty', align: 'right' },
    { title: 'Cấp tổ may', dataIndex: 'issuedToSewingQty', align: 'right' },
    { title: 'Hoàn thiện', dataIndex: 'finishedQty', align: 'right' },
    { title: 'Nhập kho TP', dataIndex: 'finishedReceivedQty', align: 'right' },
    { title: 'Đã giao', dataIndex: 'deliveredQty', align: 'right' },
    { title: 'Rework', dataIndex: 'reworkQty', align: 'right' },
    { title: 'Scrap', dataIndex: 'scrapQty', align: 'right' },
    {
      title: 'Tỉ lệ lỗi',
      dataIndex: 'defectRatePct',
      align: 'right',
      render: (v: number | null) => `${v ?? 0}%`,
    },
  ];
  return (
    <DetailModal
      open={order !== null}
      onClose={onClose}
      title={order ? `Tiến độ ${order.orderCode}` : ''}
      size="lg"
    >
      {lines.isLoading && <Spin />}
      {lines.error && <Alert type="error" message={lines.error.message} />}
      <DataTable
        rowKey="orderLineId"
        columns={columns}
        dataSource={lines.data}
        pagination={false}
      />
    </DetailModal>
  );
}

/** UC21: Admin / Quản đốc xem tiến độ cắt, may, QC, nhập kho, giao hàng theo đơn; đơn nguy cơ trễ được đánh dấu. */
export function OrderProgressPanel() {
  const role = useRolePath();
  const [includeClosed, setIncludeClosed] = useState(false);
  const [viewing, setViewing] = useState<OrderProgress | null>(null);
  const overview = useQuery({
    queryKey: ['dashboard', 'overview', role, includeClosed],
    queryFn: () => get<OrderProgress[]>(`/api/v1/${role}/order-progress`, { includeClosed }),
  });
  const risky = (overview.data ?? []).filter((o) => o.isDelayRisk);

  const columns: TableColumnsType<OrderProgress> = [
    {
      title: 'Đơn hàng',
      key: 'order',
      render: (_, o) => (
        <Space direction="vertical" size={0}>
          <DetailLink onClick={() => setViewing(o)}>{o.orderCode}</DetailLink>
          <Typography.Text type="secondary">{o.customerName}</Typography.Text>
        </Space>
      ),
    },
    {
      title: 'Hạn giao',
      key: 'due',
      render: (_, o) => (
        <Space direction="vertical" size={0}>
          {formatDate(o.dueDate)}
          <Typography.Text type={o.daysLeft < 0 ? 'danger' : 'secondary'}>
            {o.daysLeft < 0 ? `Quá hạn ${-o.daysLeft} ngày` : `Còn ${o.daysLeft} ngày`}
          </Typography.Text>
        </Space>
      ),
    },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      render: (v: string) => <StatusTag status={v} labels={ORDER_STATUS_TAG} />,
    },
    {
      title: 'Cắt',
      key: 'cut',
      render: (_, o) => <Stage value={o.cutQty} total={o.orderedQty} label="Đã cắt" />,
    },
    {
      title: 'Vào kho BTP',
      key: 'wh',
      render: (_, o) => (
        <Stage value={o.cutHandedToWarehouseQty} total={o.orderedQty} label="BTP vào kho" />
      ),
    },
    {
      title: 'Cấp tổ may',
      key: 'sew',
      render: (_, o) => (
        <Stage value={o.issuedToSewingQty} total={o.orderedQty} label="Cấp tổ may" />
      ),
    },
    {
      title: 'Hoàn thiện',
      key: 'fin',
      render: (_, o) => <Stage value={o.finishedQty} total={o.orderedQty} label="Hoàn thiện" />,
    },
    {
      title: 'Nhập kho TP',
      key: 'fg',
      render: (_, o) => (
        <Stage value={o.finishedReceivedQty} total={o.orderedQty} label="Nhập kho thành phẩm" />
      ),
    },
    {
      title: 'Đã giao',
      key: 'del',
      render: (_, o) => <Stage value={o.deliveredQty} total={o.orderedQty} label="Đã giao" />,
    },
    {
      title: 'Lỗi',
      key: 'defect',
      align: 'right',
      render: (_, o) => (
        <Tooltip title={`Rework ${o.reworkQty} · Scrap ${o.scrapQty}`}>
          <Typography.Text type={o.defectRatePct >= 5 ? 'danger' : undefined}>
            {o.defectRatePct}%
          </Typography.Text>
        </Tooltip>
      ),
    },
    {
      title: 'Cảnh báo',
      key: 'risk',
      render: (_, o) =>
        o.isDelayRisk ? (
          <Tag color="red">
            Nguy cơ trễ{o.delayRiskSince ? ` từ ${formatDate(o.delayRiskSince)}` : ''}
          </Tag>
        ) : null,
    },
  ];

  return (
    <Card title="Tiến độ đơn hàng" style={{ marginTop: 16 }}>
      <Space style={{ marginBottom: 16 }} wrap>
        <Checkbox onChange={(e) => setIncludeClosed(e.target.checked)}>Gồm đơn đã đóng</Checkbox>
      </Space>
      {risky.length > 0 && (
        <Alert
          type="error"
          showIcon
          style={{ marginBottom: 12 }}
          message={`${risky.length} đơn có nguy cơ trễ hạn: ${risky.map((o) => o.orderCode).join(', ')}`}
        />
      )}
      <QueryError error={overview.error} onRetry={() => void overview.refetch()} />
      <DataTable
        rowKey="orderId"
        columns={columns}
        dataSource={overview.data}
        loading={overview.isFetching}
        pagination={false}
      />
      <LinesDrawer order={viewing} onClose={() => setViewing(null)} />
    </Card>
  );
}
