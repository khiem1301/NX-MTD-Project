import { PlusOutlined } from '@ant-design/icons';
import { Alert, Button, Form, Space } from 'antd';
import { PageCard } from '@/components/common/PageCard';
import { StatusTag } from '@/components/common/StatusTag';
import { UnsavedChangesModal } from '@/components/common/UnsavedChangesModal';
import { MESSAGES } from '@/constants/messages';
import { ORDER_STATUS_TAG } from '@/constants/orderStatus';
import { ROUTES } from '@/constants/routes';
import { newBlock } from '../utils/orderGrid';
import { useOrderForm, type HeaderValues } from '../hooks/useOrderForm';
import { CustomerFormModal } from './CustomerFormModal';
import { OrderHeaderFields } from './OrderHeaderFields';
import { ProductBlockEditor } from './ProductBlockEditor';
import { OrderImportButton } from './OrderImportButton';
import { OrderExcelImports } from './OrderExcelImports';
import { OrderFormFooter } from './OrderFormFooter';
import { ProductFormModal } from './ProductFormModal';
import type { OrderDetail } from '../types';

export type OrderFormProps = {
  orderId: number | null;
  detail: OrderDetail | undefined;
  /** Đơn nguồn khi tạo bằng Sao chép đơn (không phải chế độ sửa). */
  copyFrom?: OrderDetail;
};

export function OrderForm({ orderId, detail, copyFrom }: OrderFormProps) {
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
    fillAddressFromCustomer,
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
    stageProduct,
    stageOntoBlock,
    unstageProduct,
    orderImports,
  } = useOrderForm({ orderId, detail, copyFrom });
  const saving = create.isPending || update.isPending;
  const goBack = () => navigate(isEdit ? ROUTES.orderDetail(orderId as number) : ROUTES.orders);
  const saveError =
    lineError ??
    (Object.keys(blockErrors).length > 0 ? MESSAGES.orders.validation.fixLines : undefined);
  const pageDescription = locked && editable ? MESSAGES.orders.lockedEditHint : undefined;

  return (
    <PageCard
      onBack={goBack}
      title={
        isEdit
          ? `${MESSAGES.orders.editOrder} ${detail?.orderCode ?? ''}`
          : MESSAGES.orders.createOrder
      }
      description={pageDescription}
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
        {copyFrom && (
          <Alert
            type="info"
            showIcon
            message={MESSAGES.orders.copyOrderHint.replace('{0}', copyFrom.orderCode)}
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
            currentCustomerId={detail?.customerId ?? copyFrom?.customerId}
            locked={locked}
            onAddCustomer={() => setCustomerOpen(true)}
            onCustomerChange={fillAddressFromCustomer}
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
          <Space.Compact block>
            <Button
              block
              type="dashed"
              icon={<PlusOutlined />}
              onClick={() => changeBlocks([...blocks, newBlock()])}
            >
              {MESSAGES.orders.addProductBlock}
            </Button>
            {/* DEV-ALL-118: đơn đã bắt đầu sản xuất thì khóa dòng hàng nên không nhập Excel. */}
            {!locked && (
              <OrderImportButton
                orderId={orderId}
                blocks={blocks}
                onStageProduct={(body, selection) => stageProduct(body, selection)}
                onUnstageProduct={unstageProduct}
                onApply={(next, newLabels) => {
                  setLabels((l) => ({ ...l, ...newLabels }));
                  changeBlocks(next);
                }}
              />
            )}
          </Space.Compact>
        )}
        {editable && (
          <OrderExcelImports orderId={orderId} imports={orderImports} disabled={saving} />
        )}
        <OrderFormFooter
          saveError={saveError}
          totals={totals}
          editable={editable}
          saving={saving}
          onCancel={goBack}
          onSave={handleSave}
        />

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
          allowImports
          onStage={(body, selection) =>
            productBlock !== undefined && stageOntoBlock(productBlock, body, selection)
          }
          onClose={() => setProductBlock(undefined)}
        />
      </div>
    </PageCard>
  );
}
