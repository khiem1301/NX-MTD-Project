import { useEffect, useRef, useState } from 'react';
import { Form, message, type UploadFile } from 'antd';
import { attachmentApi } from '@/hooks/useAttachments';
import { ATTACHMENT_OWNER, ATTACHMENT_PURPOSE } from '@/types/attachment';
import dayjs from 'dayjs';
import { MESSAGES } from '@/constants/messages';
import { MAX_PAGE_SIZE } from '@/constants/pagination';
import { API_DATE, fill, formatQty } from '@/utils/format';
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
  const lines = Form.useWatch('lines', form);
  /** Ảnh chứng từ chọn trước khi lưu: cần Id phiếu nên chỉ tải lên sau khi phiếu mới được tạo. */
  const [photos, setPhotos] = useState<UploadFile[]>([]);
  /** Vừa chọn lại đơn: khi dữ liệu đơn tải xong thì điền sẵn mọi dòng NVL còn phải nhận. */
  const fillPending = useRef(false);

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
              lines: preset.purchaseLines ?? [
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

  const resetLines = () => {
    fillPending.current = true;
    form.setFieldValue('lines', [EMPTY_LINE]);
  };

  // Chọn đơn mua / đơn hàng → điền sẵn các dòng NVL còn phải nhận, Thủ kho chỉ sửa số nhận và bỏ dòng chưa về.
  useEffect(() => {
    if (!fillPending.current) return;
    const fill = (rows: { [key: string]: number }[]) => {
      fillPending.current = false;
      form.setFieldValue('lines', rows.length > 0 ? rows : [EMPTY_LINE]);
    };
    if (isSupplier) {
      const po = purchaseOrder.data;
      if (!po || po.purchaseOrderId !== purchaseOrderId) return;
      fill(
        po.lines
          .map((l) => ({
            purchaseOrderLineId: l.purchaseOrderLineId,
            acceptedQty: l.orderedQty - l.receivedQty - l.notReplenishedQty,
          }))
          .filter((l) => l.acceptedQty > 0),
      );
    } else {
      const rows = orderMaterials.data;
      if (!rows || orderId === undefined || rows.some((om) => om.orderId !== orderId)) return;
      fill(
        rows
          .map((om) => ({
            orderMaterialId: om.orderMaterialId,
            acceptedQty: om.customerQty - om.receivedQty,
          }))
          .filter((l) => l.acceptedQty > 0),
      );
    }
  }, [isSupplier, purchaseOrder.data, purchaseOrderId, orderMaterials.data, orderId, form]);

  /** Đóng form và bỏ các ảnh chưa lưu. */
  const handleClose = () => {
    setPhotos([]);
    onClose();
  };

  const uploadPhotos = async (receiptId: number) => {
    const results = await Promise.allSettled(
      photos.map((p) =>
        attachmentApi.upload(
          ATTACHMENT_OWNER.MaterialReceipt,
          receiptId,
          p.originFileObj as File,
          ATTACHMENT_PURPOSE.DocumentPhoto,
        ),
      ),
    );
    const failed = results.filter((r) => r.status === 'rejected').length;
    if (failed > 0) void message.warning(fill(LABELS.photoUploadFailed, failed));
  };

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
    save.mutate(
      { id: receipt?.materialReceiptId, body },
      {
        onSuccess: async (saved) => {
          if (!receipt) await uploadPhotos(saved.materialReceiptId);
          handleClose();
        },
      },
    );
  };

  const customerOptions = (orderMaterials.data ?? []).map((om) => ({
    value: om.orderMaterialId,
    outstanding: om.customerQty - om.receivedQty,
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
      outstanding,
      label: `${l.materialCode} ${l.materialName}${l.materialColor ? ` - ${l.materialColor}` : ''} · ${LABELS.outstandingQty} ${formatQty(outstanding)} ${l.unit}`,
    };
  });
  const required = [{ required: true, message: MESSAGES.common.required }];

  // Dòng NVL còn phải nhận mà phiếu chưa có: chọn thêm từ danh sách thay vì thêm dòng trống.
  const chosen = new Set(
    (lines ?? []).map((l) => (isSupplier ? l.purchaseOrderLineId : l.orderMaterialId)),
  );
  const addableOptions = (isSupplier ? purchaseLineOptions : customerOptions).filter(
    (o) => !chosen.has(o.value) && !('disabled' in o && o.disabled) && o.outstanding > 0,
  );

  return {
    handleClose,
    photos,
    setPhotos,
    addableOptions,
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
