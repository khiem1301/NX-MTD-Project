import { useState } from 'react';
import { Alert, Button, Descriptions, Divider, Drawer, Result, Space, Spin, Table, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useSearchParams } from 'react-router-dom';
import { AuditHistory } from '@/components/common/AuditHistory';
import { QueryError } from '@/components/common/QueryError';
import { ReasonModal } from '@/components/common/ReasonModal';
import { StatusTag } from '@/components/common/StatusTag';
import { ERROR_CODES } from '@/constants/errorCodes';
import { MESSAGES } from '@/constants/messages';
import type { ApiError } from '@/types/api';
import { formatCurrency, formatDate, formatDateTime, formatQty } from '@/utils/format';
import {
  GOODS_STATUS_LABEL,
  PAYMENT_METHOD_LABEL,
  PAYMENT_STATUS_LABEL,
  PURCHASE_TEXT,
} from '../constants';
import {
  useCancelOrder,
  usePurchaseOrderDetail,
  useReturnPayment,
} from '../hooks/usePurchase';
import type { PurchaseOrderLine, PurchaseRole } from '../types';
import { ConfirmPaymentModal } from './ConfirmPaymentModal';
import { PaymentProofGallery } from './PaymentProofGallery';
import { PlaceOrderModal } from './PlaceOrderModal';
import { RepriceModal } from './RepriceModal';

type Props = { role: PurchaseRole };
type Dialog = 'place' | 'reprice' | 'confirm' | 'cancel' | 'return' | null;

const text = PURCHASE_TEXT.detail;

const lineColumns: ColumnsType<PurchaseOrderLine> = [
  {
    title: text.material,
    key: 'material',
    render: (_, l) => `${l.materialCode} - ${l.materialName}`,
  },
  { title: text.color, dataIndex: 'materialColor', render: (value: string) => value || '—' },
  {
    title: text.qty,
    dataIndex: 'orderedQty',
    align: 'right',
    render: (value: number, l) => `${formatQty(value)} ${l.unit}`,
  },
  { title: text.unitPrice, dataIndex: 'unitPrice', align: 'right', render: (v: number) => formatCurrency(v) },
  {
    title: text.amount,
    key: 'amount',
    align: 'right',
    render: (_, l) => formatCurrency(l.orderedQty * l.unitPrice),
  },
];

/**
 * Chi tiết đơn mua mở theo ?id=<purchaseOrderId> (link trong thông báo chia sẻ được). Nút hiện theo `permissions`
 * server trả về: Quản đốc đặt hàng / hủy / sửa đơn giá; Kế toán xác nhận thanh toán / trả lại.
 */
export function PurchaseOrderDrawer({ role }: Props) {
  const [searchParams, setSearchParams] = useSearchParams();
  const idParam = Number(searchParams.get('id'));
  const id = Number.isInteger(idParam) && idParam > 0 ? idParam : null;
  const { data, isLoading, error, refetch } = usePurchaseOrderDetail(role, id);
  const cancel = useCancelOrder(id ?? 0);
  const returnPayment = useReturnPayment(id ?? 0);
  const [dialog, setDialog] = useState<Dialog>(null);

  const close = () => setSearchParams({});
  const closeDialog = () => setDialog(null);
  const notFound = (error as ApiError | null)?.errorCode === ERROR_CODES.CommonNotFound;
  const perm = data?.permissions;

  const actions = data && perm && (
    <Space wrap>
      {perm.canPlace && (
        <Button type="primary" onClick={() => setDialog('place')}>
          {PURCHASE_TEXT.actions.place}
        </Button>
      )}
      {perm.canReprice && (
        <Button type="primary" onClick={() => setDialog('reprice')}>
          {PURCHASE_TEXT.actions.reprice}
        </Button>
      )}
      {perm.canConfirmPayment && (
        <Button type="primary" onClick={() => setDialog('confirm')}>
          {PURCHASE_TEXT.actions.confirmPayment}
        </Button>
      )}
      {perm.canReturnPayment && (
        <Button danger onClick={() => setDialog('return')}>
          {PURCHASE_TEXT.actions.returnPayment}
        </Button>
      )}
      {perm.canCancel && (
        <Button danger onClick={() => setDialog('cancel')}>
          {PURCHASE_TEXT.actions.cancel}
        </Button>
      )}
    </Space>
  );

  return (
    <Drawer
      open={id !== null}
      onClose={close}
      width={760}
      destroyOnHidden
      title={data?.poCode}
      extra={actions}
    >
      {isLoading && <Spin />}
      {notFound && <Result status="404" subTitle={MESSAGES.common.notFound} />}
      {!notFound && <QueryError error={error} onRetry={() => void refetch()} />}

      {data && (
        <>
          {data.rejectReason && (
            <Alert type="error" showIcon message={`${text.rejectReason}: ${data.rejectReason}`} style={{ marginBottom: 12 }} />
          )}
          {data.paymentStatus === 'Returned' && data.paymentReturnReason && (
            <Alert
              type="warning"
              showIcon
              message={`${text.returnReason}: ${data.paymentReturnReason}`}
              style={{ marginBottom: 12 }}
            />
          )}

          <Descriptions bordered size="small" column={{ xs: 1, sm: 2 }}>
            <Descriptions.Item label={PURCHASE_TEXT.columns.order}>
              {data.orderCode ?? PURCHASE_TEXT.columns.stock}
            </Descriptions.Item>
            <Descriptions.Item label={PURCHASE_TEXT.columns.supplier}>{data.supplierName ?? '—'}</Descriptions.Item>
            <Descriptions.Item label={PURCHASE_TEXT.columns.goodsStatus}>
              <StatusTag status={data.goodsStatus} labels={GOODS_STATUS_LABEL} />
            </Descriptions.Item>
            <Descriptions.Item label={PURCHASE_TEXT.columns.paymentStatus}>
              <StatusTag status={data.paymentStatus} labels={PAYMENT_STATUS_LABEL} />
            </Descriptions.Item>
            <Descriptions.Item label={PURCHASE_TEXT.columns.orderDate}>{formatDate(data.orderDate)}</Descriptions.Item>
            <Descriptions.Item label={text.expectedDate}>{formatDate(data.expectedDeliveryDate)}</Descriptions.Item>
            <Descriptions.Item label={text.total}>
              <Typography.Text strong>{formatCurrency(data.totalAmount)}</Typography.Text>
            </Descriptions.Item>
            <Descriptions.Item label={text.note}>{data.note ?? '—'}</Descriptions.Item>
          </Descriptions>

          <Divider orientation="left">{text.lines}</Divider>
          <Table<PurchaseOrderLine>
            rowKey="purchaseOrderLineId"
            size="small"
            columns={lineColumns}
            dataSource={data.lines}
            pagination={false}
            scroll={{ x: 'max-content' }}
          />

          {data.paymentStatus === 'Paid' && (
            <>
              <Divider orientation="left">{text.paymentInfo}</Divider>
              <Descriptions bordered size="small" column={{ xs: 1, sm: 2 }}>
                <Descriptions.Item label={text.paymentDate}>{formatDate(data.paymentDate)}</Descriptions.Item>
                <Descriptions.Item label={text.paidAmount}>{formatCurrency(data.paidAmount)}</Descriptions.Item>
                <Descriptions.Item label={text.paymentMethod}>
                  {data.paymentMethod ? PAYMENT_METHOD_LABEL[data.paymentMethod] : '—'}
                </Descriptions.Item>
                <Descriptions.Item label={text.paidAt}>{formatDateTime(data.paidAt)}</Descriptions.Item>
                <Descriptions.Item label={text.paymentNote} span={2}>
                  {data.paymentNote ?? '—'}
                </Descriptions.Item>
              </Descriptions>
            </>
          )}

          {data.goodsStatus !== 'PendingFinalisation' && data.goodsStatus !== 'Rejected' && (
            <>
              <Divider orientation="left">{text.proofs}</Divider>
              {perm?.canConfirmPayment && (
                <Typography.Paragraph type="secondary">{text.proofsHint}</Typography.Paragraph>
              )}
              <PaymentProofGallery purchaseOrderId={data.purchaseOrderId} canEdit={perm?.canConfirmPayment ?? false} />
            </>
          )}

          <Divider orientation="left">{text.history}</Divider>
          <AuditHistory history={data.history} />

          <PlaceOrderModal open={dialog === 'place'} order={data} onClose={closeDialog} />
          <RepriceModal open={dialog === 'reprice'} order={data} onClose={closeDialog} />
          <ConfirmPaymentModal open={dialog === 'confirm'} order={data} onClose={closeDialog} />
          <ReasonModal
            open={dialog === 'cancel'}
            title={PURCHASE_TEXT.cancelTitle}
            loading={cancel.isPending}
            onCancel={closeDialog}
            onSubmit={(reason) => cancel.mutate({ reason, rowVer: data.rowVer }, { onSuccess: closeDialog })}
          />
          <ReasonModal
            open={dialog === 'return'}
            title={PURCHASE_TEXT.returnTitle}
            loading={returnPayment.isPending}
            onCancel={closeDialog}
            onSubmit={(reason) => returnPayment.mutate({ reason, rowVer: data.rowVer }, { onSuccess: closeDialog })}
          />
        </>
      )}
    </Drawer>
  );
}
