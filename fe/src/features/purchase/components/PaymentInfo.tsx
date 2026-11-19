import { useState } from 'react';
import { Descriptions, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { EditOutlined } from '@ant-design/icons';
import { DataTable } from '@/components/common/DataTable';
import { IconAction } from '@/components/common/IconAction';
import { MESSAGES } from '@/constants/messages';
import { formatCurrency, formatDate, formatDateTime } from '@/utils/format';
import { PAYMENT_METHOD_LABEL, PURCHASE_TEXT } from '../constants';
import type { PurchaseOrderDetail, PurchasePayment } from '../types';
import { EditPaymentModal } from './EditPaymentModal';

const text = PURCHASE_TEXT.detail;

/**
 * Các lần thanh toán của đơn mua (mỗi lần một dòng, không xóa) kèm tổng đã trả, giá trị thực tế và số còn phải trả.
 * Sửa được một lần trong 24 giờ kể từ lúc nhập, bắt buộc lý do.
 */
export function PaymentInfo({ order: data }: { order: PurchaseOrderDetail }) {
  const [editing, setEditing] = useState<PurchasePayment | null>(null);
  const paid = data.paidAmount ?? 0;

  const columns: ColumnsType<PurchasePayment> = [
    { title: text.paymentDate, dataIndex: 'paymentDate', render: (v: string) => formatDate(v) },
    {
      title: text.paidAmount,
      dataIndex: 'amount',
      align: 'right',
      render: (v: number) => formatCurrency(v),
    },
    {
      title: text.paymentMethod,
      dataIndex: 'paymentMethod',
      render: (v: PurchasePayment['paymentMethod']) => PAYMENT_METHOD_LABEL[v],
    },
    { title: text.paymentNote, dataIndex: 'paymentNote', render: (v: string | null) => v ?? '—' },
    { title: text.enteredAt, dataIndex: 'createdAt', render: (v: string) => formatDateTime(v) },
    {
      title: text.editedNote,
      key: 'edited',
      render: (_, p) =>
        p.updatedAt ? `${formatDateTime(p.updatedAt)}: ${p.editReason ?? ''}` : '—',
    },
    {
      title: MESSAGES.common.actions,
      key: 'action',
      render: (_, p) =>
        p.canEdit ? (
          <IconAction
            title={text.editPayment}
            icon={<EditOutlined />}
            onClick={() => setEditing(p)}
          />
        ) : null,
    },
  ];

  return (
    <>
      <Descriptions bordered size="small" column={{ xs: 1, sm: 3 }} style={{ marginBottom: 12 }}>
        <Descriptions.Item label={text.paidAmount}>{formatCurrency(paid)}</Descriptions.Item>
        <Descriptions.Item label={text.actualTotal}>
          {formatCurrency(data.actualAmount)}
        </Descriptions.Item>
        <Descriptions.Item label={text.remaining}>
          <Typography.Text strong>
            {formatCurrency(Math.max(0, data.actualAmount - paid))}
          </Typography.Text>
        </Descriptions.Item>
      </Descriptions>
      <DataTable<PurchasePayment>
        rowKey="purchasePaymentId"
        size="small"
        columns={columns}
        dataSource={data.payments}
        pagination={false}
        scroll={{ x: 'max-content' }}
      />
      <EditPaymentModal order={data} payment={editing} onClose={() => setEditing(null)} />
    </>
  );
}
