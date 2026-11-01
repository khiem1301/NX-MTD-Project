import { PlusOutlined } from '@ant-design/icons';
import { Alert, Button, Form, Space, Typography, theme } from 'antd';
import type { CSSProperties } from 'react';
import { PageCard } from '@/components/common/PageCard';
import { StatusTag } from '@/components/common/StatusTag';
import { UnsavedChangesModal } from '@/components/common/UnsavedChangesModal';
import { MESSAGES } from '@/constants/messages';
import { ORDER_STATUS_TAG } from '@/constants/orderStatus';
import { ROUTES } from '@/constants/routes';
import { formatCurrency, formatQty } from '@/utils/format';
import { newBlock } from '../utils/orderGrid';
import { useOrderForm, type HeaderValues } from '../hooks/useOrderForm';
import { CustomerFormModal } from './CustomerFormModal';
import { OrderHeaderFields } from './OrderHeaderFields';
import { ProductBlockEditor } from './ProductBlockEditor';
import { ProductFormModal } from './ProductFormModal';
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
    handleSave,
    leaveGuard,
    initialValues,
    form,
    blocks,
    blockErrors,
    labels,
    setLabels,
    lineError,
    setDirty,
    customerOpen,
    setCustomerOpen,
    productBlock,
    setProductBlock,
  } = useOrderForm({ orderId, detail });
  const { token } = theme.useToken();
  const saving = create.isPending || update.isPending;
  const goBack = () => navigate(isEdit ? ROUTES.orderDetail(orderId as number) : ROUTES.orders);
  const saveError =
    lineError ??
    (Object.keys(blockErrors).length > 0 ? MESSAGES.orders.validation.fixLines : undefined);

  // Thanh tổng và nút Lưu dính đáy màn hình: đơn nhiều mã hàng không phải cuộn xuống cuối để lưu.
  const footerStyle: CSSProperties = {
    position: 'sticky',
    bottom: 0,
    zIndex: 10,
    display: 'flex',
    flexWrap: 'wrap',
    gap: 8,
    alignItems: 'center',
    justifyContent: 'space-between',
    marginTop: 12,
    padding: '12px 0',
    background: token.colorBgContainer,
    borderTop: `1px solid ${token.colorBorderSecondary}`,
  };

  return (
    <PageCard
      onBack={goBack}
      title={
        isEdit
          ? `${MESSAGES.orders.editOrder} ${detail?.orderCode ?? ''}`
          : MESSAGES.orders.createOrder
      }
      description={locked && editable ? MESSAGES.orders.lockedEditHint : undefined}
      extra={detail && <StatusTag status={detail.status} labels={ORDER_STATUS_TAG} />}
    >
      <div>
        {!editable && (
          <Alert
            type="warning"
            showIcon
            message={MESSAGES.orders.notEditable}
            style={{ marginBottom: 12 }}
          />
        )}
        <Form<HeaderValues>
          form={form}
          layout="vertical"
          disabled={!editable || saving}
          initialValues={initialValues}
          onValuesChange={() => setDirty(true)}
          onFinish={handleFinish}
          scrollToFirstError
        >
          <OrderHeaderFields
            customers={customers.data ?? []}
            currentCustomerId={detail?.customerId}
            locked={locked}
            onAddCustomer={() => setCustomerOpen(true)}
          />
        </Form>

        {blocks.map((block, index) => (
          <ProductBlockEditor
            key={block.key}
            block={block}
            locked={locked}
            productLabels={labels}
            errors={blockErrors[block.key]}
            usedProductIds={blocks
              .filter((b) => b.key !== block.key && b.productId !== null)
              .map((b) => b.productId as number)}
            onChange={(next) => changeBlocks(blocks.map((b, i) => (i === index ? next : b)))}
            onRemove={() => changeBlocks(blocks.filter((_b, i) => i !== index))}
            onCreateProduct={() => setProductBlock(block.key)}
          />
        ))}
        {editable && (
          <Button
            block
            type="dashed"
            icon={<PlusOutlined />}
            onClick={() => changeBlocks([...blocks, newBlock()])}
          >
            {MESSAGES.orders.addProductBlock}
          </Button>
        )}
        <div style={footerStyle}>
          {saveError && (
            <Alert type="error" showIcon message={saveError} style={{ width: '100%' }} />
          )}
          <Typography.Text strong>
            {`${MESSAGES.orders.totalQty}: ${formatQty(totals.qty)} · ${MESSAGES.orders.totalAmount}: ${formatCurrency(totals.amount)}`}
          </Typography.Text>
          <Space>
            <Button onClick={goBack}>{MESSAGES.common.cancel}</Button>
            <Button type="primary" disabled={!editable} loading={saving} onClick={handleSave}>
              {MESSAGES.common.save}
            </Button>
          </Space>
        </div>

        <UnsavedChangesModal
          open={leaveGuard.open}
          onLeave={leaveGuard.leave}
          onStay={leaveGuard.stay}
        />
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
      </div>
    </PageCard>
  );
}
