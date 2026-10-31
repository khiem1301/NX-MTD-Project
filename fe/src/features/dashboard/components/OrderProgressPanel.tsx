import { useState } from 'react';
import {
  Alert,
  Card,
  Checkbox,
  Space,
  Tag,
  Tooltip,
  Typography,
  type TableColumnsType,
} from 'antd';
import { DataTable } from '@/components/common/DataTable';
import { DetailLink } from '@/components/common/DetailLink';
import { QueryError } from '@/components/common/QueryError';
import { StatusTag } from '@/components/common/StatusTag';
import { ORDER_STATUS_TAG } from '@/constants/orderStatus';
import { formatDate } from '@/utils/format';
import { useOrderProgress } from '../hooks/useOrderProgress';
import type { OrderProgress } from '../types';
import { OrderLinesModal } from './OrderLinesModal';
import { StageBar } from './StageBar';

/** UC21: Admin / Quản đốc xem tiến độ cắt, may, QC, nhập kho, giao hàng theo đơn; đơn nguy cơ trễ được đánh dấu. */
export function OrderProgressPanel() {
  const [includeClosed, setIncludeClosed] = useState(false);
  const [viewing, setViewing] = useState<OrderProgress | null>(null);
  const overview = useOrderProgress(includeClosed);
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
      render: (_, o) => <StageBar value={o.cutQty} total={o.orderedQty} label="Đã cắt" />,
    },
    {
      title: 'Vào kho BTP',
      key: 'wh',
      render: (_, o) => (
        <StageBar value={o.cutHandedToWarehouseQty} total={o.orderedQty} label="BTP vào kho" />
      ),
    },
    {
      title: 'Cấp tổ may',
      key: 'sew',
      render: (_, o) => (
        <StageBar value={o.issuedToSewingQty} total={o.orderedQty} label="Cấp tổ may" />
      ),
    },
    {
      title: 'Hoàn thiện',
      key: 'fin',
      render: (_, o) => <StageBar value={o.finishedQty} total={o.orderedQty} label="Hoàn thiện" />,
    },
    {
      title: 'Nhập kho TP',
      key: 'fg',
      render: (_, o) => (
        <StageBar value={o.finishedReceivedQty} total={o.orderedQty} label="Nhập kho thành phẩm" />
      ),
    },
    {
      title: 'Đã giao',
      key: 'del',
      render: (_, o) => <StageBar value={o.deliveredQty} total={o.orderedQty} label="Đã giao" />,
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
      <OrderLinesModal order={viewing} onClose={() => setViewing(null)} />
    </Card>
  );
}
