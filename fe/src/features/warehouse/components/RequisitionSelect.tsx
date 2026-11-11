import { Select } from 'antd';
import { REQUEST_STATUS, type RequestType } from '@/constants/requestTypes';
import { useRequestInbox } from '@/hooks/useRequests';

const PAGE_SIZE = 100;

type RequisitionSelectProps = {
  /** MaterialRequisition (xin NVL, tổ cắt) hoặc SemiFinishedRequisition (xin BTP, tổ may). */
  requestType: RequestType;
  value?: number;
  onChange?: (requestId: number | undefined) => void;
  disabled?: boolean;
  placeholder?: string;
  width?: number | string;
};

/** Đơn xin NVL / BTP đã duyệt, đang chờ Thủ kho cấp; lập phiếu cấp xong đơn tự chuyển sang Hoàn thành. */
export function RequisitionSelect({
  requestType,
  value,
  onChange,
  disabled,
  placeholder,
  width = 340,
}: RequisitionSelectProps) {
  const requests = useRequestInbox({
    page: 1,
    pageSize: PAGE_SIZE,
    requestType,
    status: REQUEST_STATUS.Approved,
  });
  return (
    <Select
      showSearch
      allowClear
      disabled={disabled}
      placeholder={placeholder}
      value={value}
      onChange={onChange}
      loading={requests.isLoading}
      optionFilterProp="label"
      style={{ width }}
      options={(requests.data?.items ?? []).map((r) => ({
        value: r.requestId,
        label: [r.requestCode, r.title, r.teamName].filter(Boolean).join(' · '),
      }))}
    />
  );
}
