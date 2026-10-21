import { PlusOutlined } from '@ant-design/icons';
import { Alert, Button, Card, DatePicker, Divider, Form, Input, Select, Space, Spin, Typography } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { useEffect, useMemo, useState } from 'react';
import { useBlocker, useNavigate, useParams } from 'react-router-dom';
import { QueryError } from '@/components/common/QueryError';
import { ERROR_CODES } from '@/constants/errorCodes';
import { MESSAGES } from '@/constants/messages';
import { ORDER_LIMITED_EDIT_STATUSES } from '@/constants/orderStatus';
import { ROUTES } from '@/constants/routes';
import type { ApiError } from '@/types/api';
import { formatCurrency, formatQty, toApiDate } from '@/utils/format';
import { CustomerFormModal } from '../components/CustomerFormModal';
import { ProductBlockEditor } from '../components/ProductBlockEditor';
import { ProductFormModal } from '../components/ProductFormModal';
import { useCreateOrder, useCustomers, useNextOrderCode, useOrder, useUpdateOrder } from '../hooks/useOrders';
import type { OrderDetail } from '../types';
import { blocksTotals, fromLines, newBlock, toLines, type ProductBlock } from '../utils/orderGrid';

type HeaderValues = { orderCode: string; customerId: number; receivedDate: Dayjs; dueDate: Dayjs; note?: string };

const LINE_ERROR_CODES: string[] = [
  ERROR_CODES.OrderLineRemoveNotAllowed,
  ERROR_CODES.OrderLineKeyLocked,
  ERROR_CODES.ProductNotActive,
  ERROR_CODES.CommonDuplicate,
];

/** UC09: tạo / sửa đơn hàng nhiều màu × size. Trạng thái khác "Mới" thì dòng cũ bị khóa (D27). */
export function OrderFormPage() {
  const { id } = useParams();
  const orderId = id ? Number(id) : null;
  const order = useOrder(orderId);

  if (orderId !== null && order.isLoading) return <Spin />;
  if (orderId !== null && order.error) return <QueryError error={order.error} onRetry={() => void order.refetch()} />;
  // key theo rowVer: sau khi dữ liệu đổi (vd. tải lại do xung đột) form khởi tạo lại từ bản mới.
  return <OrderForm key={order.data?.rowVer ?? 'new'} orderId={orderId} detail={order.data} />;
}

type OrderFormProps = { orderId: number | null; detail: OrderDetail | undefined };

function OrderForm({ orderId, detail }: OrderFormProps) {
  const isEdit = orderId !== null;
  const navigate = useNavigate();
  const [form] = Form.useForm<HeaderValues>();
  const nextCode = useNextOrderCode(!isEdit);
  const customers = useCustomers();
  const create = useCreateOrder();
  const update = useUpdateOrder(orderId ?? 0);

  const [blocks, setBlocks] = useState<ProductBlock[]>(() => (detail ? fromLines(detail.lines) : [newBlock()]));
  const [labels, setLabels] = useState<Record<number, string>>(() =>
    Object.fromEntries((detail?.lines ?? []).map((l) => [l.productId, `${l.productCode} · ${l.productName}`])),
  );
  const [lineError, setLineError] = useState<string>();
  const [dirty, setDirty] = useState(false);
  const [customerOpen, setCustomerOpen] = useState(false);
  const [productBlock, setProductBlock] = useState<string>();
  const locked = isEdit && detail !== undefined && detail.status !== 'New';
  const editable = !isEdit || (detail !== undefined && (detail.status === 'New' || ORDER_LIMITED_EDIT_STATUSES.includes(detail.status)));

  useEffect(() => {
    if (!isEdit && nextCode.data) form.setFieldsValue({ orderCode: nextCode.data });
  }, [isEdit, nextCode.data, form]);

  const blocker = useBlocker(({ currentLocation, nextLocation }) => dirty && currentLocation.pathname !== nextLocation.pathname);
  useEffect(() => {
    if (blocker.state !== 'blocked') return;
    if (window.confirm(MESSAGES.orders.unsavedChanges)) blocker.proceed();
    else blocker.reset();
  }, [blocker]);

  const totals = useMemo(() => blocksTotals(blocks), [blocks]);

  const changeBlocks = (next: ProductBlock[]) => {
    setBlocks(next);
    setDirty(true);
    setLineError(undefined);
  };

  const handleError = (error: ApiError) => {
    if (error.errorCode === ERROR_CODES.OrderCodeDuplicate) form.setFields([{ name: 'orderCode', errors: [error.message] }]);
    else if (LINE_ERROR_CODES.includes(error.errorCode)) setLineError(error.message);
  };

  const handleFinish = (values: HeaderValues) => {
    const lines = toLines(blocks);
    if (lines.length === 0) {
      setLineError(MESSAGES.orders.needLine);
      return;
    }
    const body = {
      orderCode: values.orderCode.trim(),
      customerId: values.customerId,
      receivedDate: toApiDate(values.receivedDate),
      dueDate: toApiDate(values.dueDate),
      note: values.note?.trim() || null,
      lines,
    };
    setDirty(false);
    const done = (savedId: number) => navigate(ROUTES.orderDetail(savedId));
    if (isEdit && detail) {
      update.mutate({ ...body, rowVer: detail.rowVer }, { onSuccess: () => done(detail.orderId), onError: (e) => { setDirty(true); handleError(e); } });
    } else {
      create.mutate(body, { onSuccess: (newId) => done(newId as number), onError: (e) => { setDirty(true); handleError(e); } });
    }
  };

  return (
    <Card title={isEdit ? `${MESSAGES.orders.editOrder} ${detail?.orderCode ?? ''}` : MESSAGES.orders.createOrder}>
      {!editable && <Alert type="warning" showIcon message="Đơn hàng ở trạng thái này không sửa được." style={{ marginBottom: 16 }} />}
      <Form<HeaderValues>
        form={form}
        layout="vertical"
        disabled={!editable || create.isPending || update.isPending}
        initialValues={
          detail
            ? {
                orderCode: detail.orderCode,
                customerId: detail.customerId,
                receivedDate: dayjs(detail.receivedDate),
                dueDate: dayjs(detail.dueDate),
                note: detail.note ?? undefined,
              }
            : { receivedDate: dayjs() }
        }
        onValuesChange={() => setDirty(true)}
        onFinish={handleFinish}
      >
        <Space wrap align="start" size="large">
          <Form.Item
            name="orderCode"
            label={MESSAGES.orders.orderCode}
            rules={[{ required: true, whitespace: true, message: MESSAGES.common.required }, { max: 30 }]}
          >
            <Input disabled={locked} style={{ width: 180 }} />
          </Form.Item>
          <Form.Item name="customerId" label={MESSAGES.orders.customer} rules={[{ required: true, message: MESSAGES.common.required }]}>
            <Select
              showSearch
              optionFilterProp="label"
              disabled={locked}
              style={{ width: 280 }}
              options={(customers.data ?? []).map((c) => ({ value: c.customerId, label: c.customerName }))}
              dropdownRender={(menu) => (
                <>
                  {menu}
                  <Divider style={{ margin: '8px 0' }} />
                  <Button type="link" icon={<PlusOutlined />} onClick={() => setCustomerOpen(true)}>{MESSAGES.orders.addCustomer}</Button>
                </>
              )}
            />
          </Form.Item>
          <Form.Item name="receivedDate" label={MESSAGES.orders.receivedDate} rules={[{ required: true, message: MESSAGES.common.required }]}>
            <DatePicker format="DD/MM/YYYY" disabled={locked} />
          </Form.Item>
          <Form.Item
            name="dueDate"
            label={MESSAGES.orders.dueDate}
            dependencies={['receivedDate']}
            rules={[
              { required: true, message: MESSAGES.common.required },
              ({ getFieldValue }) => ({
                validator: (_, value: Dayjs | undefined) =>
                  !value || !getFieldValue('receivedDate') || !value.isBefore(getFieldValue('receivedDate') as Dayjs, 'day')
                    ? Promise.resolve()
                    : Promise.reject(new Error('Ngày giao phải sau hoặc bằng ngày nhận.')),
              }),
            ]}
          >
            <DatePicker format="DD/MM/YYYY" />
          </Form.Item>
        </Space>
        <Form.Item name="note" label={MESSAGES.orders.note} rules={[{ max: 1000 }]}>
          <Input.TextArea rows={2} />
        </Form.Item>
      </Form>

      {lineError && <Alert type="error" showIcon message={lineError} style={{ marginBottom: 16 }} />}
      {blocks.map((block, index) => (
        <ProductBlockEditor
          key={block.key}
          block={block}
          locked={locked}
          productLabels={labels}
          onChange={(next) => changeBlocks(blocks.map((b, i) => (i === index ? next : b)))}
          onRemove={() => changeBlocks(blocks.filter((_b, i) => i !== index))}
          onCreateProduct={() => setProductBlock(block.key)}
        />
      ))}
      {editable && (
        <Button icon={<PlusOutlined />} onClick={() => changeBlocks([...blocks, newBlock()])}>
          {MESSAGES.orders.addProductBlock}
        </Button>
      )}
      <Divider />
      <Space style={{ width: '100%', justifyContent: 'space-between' }} wrap>
        <Typography.Text strong>
          {`${MESSAGES.orders.totalQty}: ${formatQty(totals.qty)} · ${MESSAGES.orders.totalAmount}: ${formatCurrency(totals.amount)}`}
        </Typography.Text>
        <Space>
          <Button onClick={() => navigate(isEdit ? ROUTES.orderDetail(orderId) : ROUTES.orders)}>{MESSAGES.common.cancel}</Button>
          <Button type="primary" disabled={!editable} loading={create.isPending || update.isPending} onClick={() => form.submit()}>
            {MESSAGES.common.save}
          </Button>
        </Space>
      </Space>

      <CustomerFormModal open={customerOpen} customer={null} onClose={() => setCustomerOpen(false)} onSaved={(customerId) => { form.setFieldValue('customerId', customerId); setDirty(true); }} />
      <ProductFormModal
        open={productBlock !== undefined}
        product={null}
        onClose={() => setProductBlock(undefined)}
        onSaved={(productId, code) => {
          setLabels((l) => ({ ...l, [productId]: code }));
          changeBlocks(blocks.map((b) => (b.key === productBlock ? { ...b, productId } : b)));
        }}
      />
    </Card>
  );
}
