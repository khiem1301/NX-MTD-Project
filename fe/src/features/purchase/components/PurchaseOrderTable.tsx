import { Tag } from 'antd';
import type { ColumnsType, TablePaginationConfig } from 'antd/es/table';
import { StatusTag } from '@/components/common/StatusTag';
import type { Paginated } from '@/types/api';
import { formatCurrency, formatDate } from '@/utils/format';
import { GOODS_STATUS_LABEL, PAYMENT_STATUS_LABEL, PURCHASE_TEXT } from '../constants';
import type { PurchaseOrderListItem } from '../types';
import { DetailLink } from '@/components/common/DetailLink';
import { DataTable } from '@/components/common/DataTable';

type Props = {
  data: Paginated<PurchaseOrderListItem> | undefined;
  loading: boolean;
  pagination: TablePaginationConfig;
  onOpen: (id: number) => void;
};

const text = PURCHASE_TEXT.columns;

/** Bảng đơn mua dùng chung cho Quản đốc và Kế toán; chi tiết mở bằng Modal theo ?id=. */
export function PurchaseOrderTable({ data, loading, pagination, onOpen }: Props) {
  const columns: ColumnsType<PurchaseOrderListItem> = [
    {
      title: text.poCode,
      dataIndex: 'poCode',
      render: (value: string, row) => (
        <DetailLink onClick={() => onOpen(row.purchaseOrderId)}>{value}</DetailLink>
      ),
    },
    {
      title: text.order,
      dataIndex: 'orderCode',
      render: (value: string | null) => value ?? <Tag>{text.stock}</Tag>,
    },
    {
      title: text.supplier,
      dataIndex: 'supplierName',
      render: (value: string | null) => value ?? '—',
    },
    {
      title: text.goodsStatus,
      dataIndex: 'goodsStatus',
      render: (value: string) => <StatusTag status={value} labels={GOODS_STATUS_LABEL} />,
    },
    {
      title: text.paymentStatus,
      dataIndex: 'paymentStatus',
      render: (value: string) => <StatusTag status={value} labels={PAYMENT_STATUS_LABEL} />,
    },
    {
      title: text.orderDate,
      dataIndex: 'orderDate',
      render: (value: string | null) => formatDate(value),
    },
    {
      title: text.total,
      dataIndex: 'totalAmount',
      align: 'right',
      render: (value: number) => formatCurrency(value),
    },
    {
      title: text.paid,
      dataIndex: 'paidAmount',
      align: 'right',
      render: (value: number | null) => formatCurrency(value),
    },
  ];

  return (
    <DataTable<PurchaseOrderListItem>
      rowKey="purchaseOrderId"
      size="small"
      columns={columns}
      dataSource={data?.items ?? []}
      loading={loading}
      pagination={pagination}
      scroll={{ x: 'max-content' }}
    />
  );
}
