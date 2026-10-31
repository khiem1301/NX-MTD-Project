import { PlusOutlined } from '@ant-design/icons';
import { Alert, Button, Card, Divider, Form, Input, Select, Space, Typography } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { ROUTES } from '@/constants/routes';
import { formatCurrency, formatQty } from '@/utils/format';
import type { Dayjs } from 'dayjs';
import { newBlock } from '../utils/orderGrid';
import { useOrderForm, type HeaderValues } from '../hooks/useOrderForm';
import { CustomerFormModal } from './CustomerFormModal';
import { ProductBlockEditor } from './ProductBlockEditor';
import { ProductFormModal } from './ProductFormModal';
import { AppDatePicker } from '@/components/common/DateInputs';
import type { OrderDetail } from '../types';

export type OrderFormProps = { orderId: number | null; detail: OrderDetail | undefined };

export function OrderForm({ orderId, detail }: OrderFormProps) {
  const {
    isEdit,
    navigate,
    customers,
    create,
    update,
    locked,
    editable,
    totals,
    changeBlocks,
    handleFinish,
    initialValues,
    form,
    blocks,
    labels,
    setLabels,
    lineError,
    setDirty,
    customerOpen,
    setCustomerOpen,
    productBlock,
    setProductBlock,
  } = useOrderForm({ orderId, detail });

  return (
    <Card
      title={
        isEdit
          ? `${MESSAGES.orders.editOrder} ${detail?.orderCode ?? ''}`
          : MESSAGES.orders.createOrder
      }
    >
      {!editable && (
        <Alert
          type="warning"
          showIcon
          message="Đơn hàng ở trạng thái này không sửa được."
          style={{ marginBottom: 16 }}
        />
      )}
      <Form<HeaderValues>
        form={form}
        layout="vertical"
        disabled={!editable || create.isPending || update.isPending}
        initialValues={initialValues}
        onValuesChange={() => setDirty(true)}
        onFinish={handleFinish}
      >
        <Space wrap align="start" size="large">
          <Form.Item
            name="orderCode"
            label={MESSAGES.orders.orderCode}
            rules={[
              { required: true, whitespace: true, message: MESSAGES.common.required },
              { max: 30 },
            ]}
          >
            <Input disabled={locked} style={{ width: 180 }} />
          </Form.Item>
          <Form.Item
            name="customerId"
            label={MESSAGES.orders.customer}
            rules={[{ required: true, message: MESSAGES.common.required }]}
          >
            <Select
              showSearch
              optionFilterProp="label"
              disabled={locked}
              style={{ width: 280 }}
              options={(customers.data ?? [])
                .filter((c) => c.status === 'Active' || c.customerId === detail?.customerId)
                .map((c) => ({ value: c.customerId, label: c.customerName }))}
              dropdownRender={(menu) => (
                <>
                  {menu}
                  <Divider style={{ margin: '8px 0' }} />
                  <Button type="link" icon={<PlusOutlined />} onClick={() => setCustomerOpen(true)}>
                    {MESSAGES.orders.addCustomer}
                  </Button>
                </>
              )}
            />
          </Form.Item>
          <Form.Item
            name="receivedDate"
            label={MESSAGES.orders.receivedDate}
            rules={[{ required: true, message: MESSAGES.common.required }]}
          >
            <AppDatePicker disabled={locked} />
          </Form.Item>
          <Form.Item
            name="dueDate"
            label={MESSAGES.orders.dueDate}
            dependencies={['receivedDate']}
            rules={[
              { required: true, message: MESSAGES.common.required },
              ({ getFieldValue }) => ({
                validator: (_, value: Dayjs | undefined) =>
                  !value ||
                  !getFieldValue('receivedDate') ||
                  !value.isBefore(getFieldValue('receivedDate') as Dayjs, 'day')
                    ? Promise.resolve()
                    : Promise.reject(new Error('Ngày giao phải sau hoặc bằng ngày nhận.')),
              }),
            ]}
          >
            <AppDatePicker />
          </Form.Item>
        </Space>
        <Form.Item name="note" label={MESSAGES.orders.note} rules={[{ max: 1000 }]}>
          <Input.TextArea rows={2} />
        </Form.Item>
      </Form>

      {lineError && (
        <Alert type="error" showIcon message={lineError} style={{ marginBottom: 16 }} />
      )}
      {blocks.map((block, index) => (
        <ProductBlockEditor
          key={block.key}
          block={block}
          locked={locked}
          productLabels={labels}
          usedProductIds={blocks
            .filter((b) => b.key !== block.key && b.productId !== null)
            .map((b) => b.productId as number)}
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
          <Button
            onClick={() => navigate(isEdit ? ROUTES.orderDetail(orderId as number) : ROUTES.orders)}
          >
            {MESSAGES.common.cancel}
          </Button>
          <Button
            type="primary"
            disabled={!editable}
            loading={create.isPending || update.isPending}
            onClick={() => form.submit()}
          >
            {MESSAGES.common.save}
          </Button>
        </Space>
      </Space>

      <CustomerFormModal
        open={customerOpen}
        customer={null}
        onClose={() => setCustomerOpen(false)}
        onSaved={(customerId) => {
          form.setFieldValue('customerId', customerId);
          setDirty(true);
        }}
      />
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
