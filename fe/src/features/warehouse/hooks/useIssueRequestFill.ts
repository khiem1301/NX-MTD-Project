import { useState } from 'react';
import { message, type FormInstance } from 'antd';
import { useQueryClient } from '@tanstack/react-query';
import { REQUESTS_KEY } from '@/hooks/useRequests';
import { requestApi } from '@/services/requestApi';
import type { RequestDetail } from '@/types/request';
import { LABELS } from '../constants';
import { pieceKey } from '../utils/pieceKey';

const toLines = (request: RequestDetail, isMaterial: boolean): Record<string, unknown>[] =>
  request.lines.flatMap((l): Record<string, unknown>[] => {
    if (isMaterial)
      return l.materialId === null ? [] : [{ materialId: l.materialId, issuedQty: l.quantity }];
    return l.orderLineId === null
      ? []
      : [
          {
            pieceKey: pieceKey(l.orderLineId, l.productComponentMaterialId),
            issuedQty: l.quantity,
          },
        ];
  });

/**
 * Chọn đơn xin trong form lập phiếu cấp: điền sẵn tổ nhận, đơn hàng, lệnh cắt (đơn xin của tổ cắt gắn lệnh) và các dòng với số lượng
 * trong đơn. Đơn xin thuộc lệnh cắt khác với lệnh đã chọn thì bỏ chọn đơn (BE cũng chặn trường hợp này).
 */
export const useIssueRequestFill = (form: FormInstance, isMaterial: boolean) => {
  const queryClient = useQueryClient();
  const [request, setRequest] = useState<RequestDetail | null>(null);

  const clear = () => setRequest(null);

  const choose = async (requestId: number | undefined) => {
    if (requestId === undefined) return clear();
    const detail = await queryClient.fetchQuery({
      queryKey: [...REQUESTS_KEY, 'detail', requestId],
      queryFn: () => requestApi.detail(requestId),
    });
    const chosenWorkOrder = form.getFieldValue('workOrderId') as number | undefined;
    if (
      isMaterial &&
      detail.workOrderId !== null &&
      chosenWorkOrder !== undefined &&
      chosenWorkOrder !== detail.workOrderId
    ) {
      form.setFieldValue('requestId', undefined);
      void message.warning(LABELS.requestWorkOrderMismatch);
      return;
    }
    setRequest(detail);
    form.setFieldsValue({
      requestId,
      workOrderId: isMaterial ? (detail.workOrderId ?? chosenWorkOrder) : undefined,
      orderId: detail.orderId ?? (form.getFieldValue('orderId') as number | undefined),
      receivingTeamId: detail.createdByTeamId ?? undefined,
      lines: toLines(detail, isMaterial),
    });
  };

  return { request, choose, clear };
};
