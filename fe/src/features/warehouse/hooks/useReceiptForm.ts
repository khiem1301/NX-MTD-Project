import { useEffect } from 'react';
import { Form } from 'antd';
import dayjs from 'dayjs';
import { MESSAGES } from '@/constants/messages';
import { MAX_PAGE_SIZE } from '@/constants/pagination';
import { API_DATE, formatQty } from '@/utils/format';
import { LABELS, MATERIAL_SOURCE } from '../constants';
import { useOrderMaterials } from './useOrderMaterials';
import { usePurchaseOrder, usePurchaseOrders } from './usePurchaseOrders';
import { useSaveReceipt } from './useReceipts';
import type { SaveReceiptPayload } from '../types';
import type { ReceiptFormModalProps } from '../components/ReceiptFormModal';
import { type Dayjs } from 'dayjs';

/** Trạng thái và xử lý của ReceiptFormModal; component chỉ lo giao diện. */
export function useReceiptForm(
  props: Pick<ReceiptFormModalProps, 'open' | 'receipt' | 'preset' | 'onClose'>,
) {
  const { open, receipt, preset, onClose } = props;
  const [form] = Form.useForm<FormValues>();
  const save = useSaveReceipt();
  const source = Form.useWatch('source', form) ?? MATERIAL_SOURCE.CUSTOMER;
  const orderId = Form.useWatch('orderId', form);
  const purchaseOrderId = Form.useWatch('purchaseOrderId', form);
  const isSupplier = source === MATERIAL_SOURCE.SUPPLIER;
  const orderMaterials = useOrderMaterials(isSupplier ? undefined : orderId);
  const purchaseOrders = usePurchaseOrders({ receivable: true, page: 1, pageSize: MAX_PAGE_SIZE });
  const purchaseOrder = usePurchaseOrder(isSupplier ? purchaseOrderId : undefined);

  useEffect(() => {
    if (!open) return;
    form.resetFields();
    form.setFieldsValue(
      receipt
        ? {
            source: receipt.source,
            orderId: receipt.orderId ?? undefined,
            purchaseOrderId: receipt.purchaseOrderId ?? undefined,
            note: receipt.note ?? undefined,
            receiptDate: dayjs(receipt.receiptDate),
            lines: receipt.lines.map((l) => ({
              orderMaterialId: l.orderMaterialId ?? undefined,
              purchaseOrderLineId: l.purchaseOrderLineId ?? undefined,
              acceptedQty: l.acceptedQty,
            })),
          }
        : preset
          ? {
              source: preset.source,
              orderId: preset.orderId,
              purchaseOrderId: preset.purchaseOrderId,
              receiptDate: dayjs(),
              lines: [
                {
                  orderMaterialId: preset.orderMaterialId,
                  purchaseOrderLineId: preset.purchaseOrderLineId,
                  acceptedQty: preset.acceptedQty,
                },
              ],
            }
          : { source: MATERIAL_SOURCE.CUSTOMER, receiptDate: dayjs(), lines: [EMPTY_LINE] },
    );
  }, [open, receipt, preset, form]);

  const resetLines = () => form.setFieldValue('lines', [EMPTY_LINE]);

  const handleFinish = (values: FormValues) => {
    const supplier = values.source === MATERIAL_SOURCE.SUPPLIER;
    const body: SaveReceiptPayload = {
      source: values.source,
      orderId: supplier ? undefined : values.orderId,
      purchaseOrderId: supplier ? values.purchaseOrderId : undefined,
      note: values.note,
      receiptDate: values.receiptDate.format(API_DATE),
      lines: values.lines.map((l) => ({
        orderMaterialId: supplier ? undefined : l.orderMaterialId,
        purchaseOrderLineId: supplier ? l.purchaseOrderLineId : undefined,
        acceptedQty: l.acceptedQty,
      })),
    };
    save.mutate({ id: receipt?.materialReceiptId, body }, { onSuccess: onClose });
  };

  const customerOptions = (orderMaterials.data ?? []).map((om) => ({
    value: om.orderMaterialId,
    label: `${om.materialCode} ${om.materialName}${om.materialColor ? ` - ${om.materialColor}` : ''} (${om.unit})`,
  }));
  const purchaseOrderOptions = (purchaseOrders.data?.items ?? []).map((po) => ({
    value: po.purchaseOrderId,
    label: `${po.poCode} · ${po.orderCode ?? LABELS.stockPurchaseTag} · ${po.supplierName ?? '—'}`,
  }));
  const purchaseLineOptions = (purchaseOrder.data?.lines ?? []).map((l) => {
    const outstanding = l.orderedQty - l.receivedQty - l.notReplenishedQty;
    return {
      value: l.purchaseOrderLineId,
      disabled: outstanding <= 0,
      label: `${l.materialCode} ${l.materialName}${l.materialColor ? ` - ${l.materialColor}` : ''} · ${LABELS.outstandingQty} ${formatQty(outstanding)} ${l.unit}`,
    };
  });
  const required = [{ required: true, message: MESSAGES.common.required }];

  return {
    save,
    source,
    orderId,
    purchaseOrderId,
    isSupplier,
    purchaseOrders,
    resetLines,
    handleFinish,
    customerOptions,
    purchaseOrderOptions,
    purchaseLineOptions,
    required,
    form,
  };
}

type FormValues = Omit<SaveReceiptPayload, 'receiptDate'> & { receiptDate: Dayjs };

export const EMPTY_LINE = { acceptedQty: 0 };
