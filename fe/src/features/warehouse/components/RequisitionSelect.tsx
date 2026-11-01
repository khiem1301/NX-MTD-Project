import { Select } from 'antd';
import { useQuery } from '@tanstack/react-query';
import { requestApi } from '@/features/requests';
import type { RequestType } from '@/constants/requestTypes';

const PAGE_SIZE = 100;
const PENDING = 'PendingProcessing';

type RequisitionSelectProps = {
  /** MaterialRequisition (xin NVL, tổ cắt) hoặc SemiFinishedRequisition (xin BTP, tổ may). */
  requestType: RequestType;
  value?: number;
  onChange?: (requestId: number | undefined) => void;
  disabled?: boolean;
};

/** Đơn xin NVL / BTP đang chờ Thủ kho xử lý; lập phiếu cấp xong đơn tự chuyển sang Hoàn thành. */
export function RequisitionSelect({
  requestType,
  value,
  onChange,
  disabled,
}: RequisitionSelectProps) {
  const requests = useQuery({
    queryKey: ['requests', 'requisitions', requestType],
    queryFn: () => requestApi.inbox({ page: 1, pageSize: PAGE_SIZE, requestType, status: PENDING }),
  });
  return (
    <Select
      showSearch
      allowClear
      disabled={disabled}
      value={value}
      onChange={onChange}
      loading={requests.isLoading}
      optionFilterProp="label"
      style={{ width: 340 }}
      options={(requests.data?.items ?? []).map((r) => ({
        value: r.requestId,
        label: [r.requestCode, r.title, r.teamName].filter(Boolean).join(' · '),
      }))}
    />
  );
}
