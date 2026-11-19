import { useState } from 'react';
import { Button, Descriptions, Divider, Result, Space, Spin, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { EditOutlined } from '@ant-design/icons';
import { useSearchParams } from 'react-router-dom';
import { AuditHistoryTable } from '@/components/common/AuditHistoryTable';
import { DataTable } from '@/components/common/DataTable';
import { DetailModal } from '@/components/common/DetailModal';
import { IconAction } from '@/components/common/IconAction';
import { QueryError } from '@/components/common/QueryError';
import { StatusTag } from '@/components/common/StatusTag';
import { ERROR_CODES } from '@/constants/errorCodes';
import { MESSAGES } from '@/constants/messages';
import type { ApiError } from '@/types/api';
import { formatCurrency, formatDate, formatDateTime } from '@/utils/format';
import {
  ORDER_PAYMENT_METHOD_LABEL,
  ORDER_PAYMENT_STATE_LABEL,
  ORDER_PAYMENT_TEXT,
} from '../constants';
import { useOrderPaymentDetail } from '../hooks/useOrderPayment';
import type { OrderPaymentLine } from '../types';
import { remainingLabel } from '../utils/remaining';
import { FinalAmountModal } from './FinalAmountModal';
import { PaymentFormModal } from './PaymentFormModal';

const text = ORDER_PAYMENT_TEXT.detail;

type Dialog = { kind: 'final' } | { kind: 'payment'; editing: OrderPaymentLine | null } | null;

/**
 * Chi tiết thanh toán một đơn hàng, mở theo ?id=<orderId>: giá trị chốt, tổng đã thanh toán, số còn lại (có dấu) và các lần
 * thanh toán. Không xóa được lần thanh toán; sửa trong 24 giờ kèm lý do.
 */
export function OrderPaymentModal() {
  const [searchParams, setSearchParams] = useSearchParams();
  const idParam = Number(searchParams.get('id'));
  const id = Number.isInteger(idParam) && idParam > 0 ? idParam : null;
  const { data, isLoading, error, refetch } = useOrderPaymentDetail(id);
  const [dialog, setDialog] = useState<Dialog>(null);
  const closeDialog = () => setDialog(null);
  const notFound = (error as ApiError | null)?.errorCode === ERROR_CODES.CommonNotFound;

  const columns: ColumnsType<OrderPaymentLine> = [
    {
      title: ORDER_PAYMENT_TEXT.pay.date,
      dataIndex: 'paymentDate',
      render: (v: string) => formatDate(v),
    },
    {
      title: ORDER_PAYMENT_TEXT.pay.amount,
      dataIndex: 'amount',
      align: 'right',
      render: (v: number) => (
        <Typography.Text type={v < 0 ? 'danger' : undefined}>{formatCurrency(v)}</Typography.Text>
      ),
    },
    {
      title: ORDER_PAYMENT_TEXT.pay.method,
      dataIndex: 'paymentMethod',
      render: (v: OrderPaymentLine['paymentMethod']) => ORDER_PAYMENT_METHOD_LABEL[v],
    },
    {
      title: ORDER_PAYMENT_TEXT.pay.note,
      dataIndex: 'paymentNote',
      render: (v: string | null) => v ?? '—',
    },
    { title: text.enteredAt, dataIndex: 'createdAt', render: (v: string) => formatDateTime(v) },
    {
      title: text.edited,
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
            title={ORDER_PAYMENT_TEXT.actions.editPayment}
            icon={<EditOutlined />}
            onClick={() => setDialog({ kind: 'payment', editing: p })}
          />
        ) : null,
    },
  ];

  const actions = data && (
    <Space wrap>
      <Button onClick={() => setDialog({ kind: 'final' })}>
        {ORDER_PAYMENT_TEXT.actions.setFinal}
      </Button>
      <Button
        type="primary"
        disabled={data.finalAmount === null}
        onClick={() => setDialog({ kind: 'payment', editing: null })}
      >
        {ORDER_PAYMENT_TEXT.actions.addPayment}
      </Button>
    </Space>
  );

  return (
    <DetailModal
      open={id !== null}
      onClose={() => setSearchParams({})}
      title={data?.orderCode}
      extra={actions}
    >
      {isLoading && <Spin />}
      {notFound && <Result status="404" subTitle={MESSAGES.common.notFound} />}
      {!notFound && <QueryError error={error} onRetry={() => void refetch()} />}
      {data && (
        <>
          <Descriptions bordered size="small" column={{ xs: 1, sm: 2 }}>
            <Descriptions.Item label={ORDER_PAYMENT_TEXT.columns.customer}>
              {data.customerName}
            </Descriptions.Item>
            <Descriptions.Item label={ORDER_PAYMENT_TEXT.columns.state}>
              <StatusTag status={data.state} labels={ORDER_PAYMENT_STATE_LABEL} />
            </Descriptions.Item>
            <Descriptions.Item label={text.finalAmount}>
              {data.finalAmount === null ? text.notSet : formatCurrency(data.finalAmount)}
            </Descriptions.Item>
            <Descriptions.Item label={text.paid}>
              {formatCurrency(data.paidAmount)}
            </Descriptions.Item>
            <Descriptions.Item label={text.remaining} span={2}>
              <Typography.Text strong>{remainingLabel(data.remaining)}</Typography.Text>
            </Descriptions.Item>
            {data.finalAmountReason && (
              <Descriptions.Item label={text.finalReason} span={2}>
                {data.finalAmountReason}
              </Descriptions.Item>
            )}
          </Descriptions>

          <Divider orientation="left">{text.payments}</Divider>
          {data.finalAmount === null && (
            <Typography.Paragraph type="secondary">{text.needFinal}</Typography.Paragraph>
          )}
          <DataTable<OrderPaymentLine>
            rowKey="orderPaymentId"
            size="small"
            columns={columns}
            dataSource={data.payments}
            pagination={false}
            locale={{ emptyText: text.noPayments }}
            scroll={{ x: 'max-content' }}
          />

          <Divider orientation="left">{text.history}</Divider>
          <AuditHistoryTable history={data.history} />

          <FinalAmountModal order={data} open={dialog?.kind === 'final'} onClose={closeDialog} />
          <PaymentFormModal
            order={data}
            open={dialog?.kind === 'payment'}
            editing={dialog?.kind === 'payment' ? dialog.editing : null}
            onClose={closeDialog}
          />
        </>
      )}
    </DetailModal>
  );
}
