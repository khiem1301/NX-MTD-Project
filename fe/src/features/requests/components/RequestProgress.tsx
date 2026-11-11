import { Steps } from 'antd';
import { MESSAGES } from '@/constants/messages';
import type { RequestStatus, RequestType } from '@/constants/requestTypes';
import { APPROVAL_TYPES } from '../constants';

type RequestProgressProps = { requestType: RequestType; status: RequestStatus };

/** Đơn đang ở bước nào: Đã gửi → Duyệt (loại có duyệt) → Hoàn thành; bị từ chối / hủy thì đánh dấu đỏ ở bước dừng. */
export function RequestProgress({ requestType, status }: RequestProgressProps) {
  const approval = APPROVAL_TYPES.includes(requestType);
  const titles = [
    MESSAGES.requests.stepSent,
    ...(approval ? [MESSAGES.requests.stepApprove] : []),
    MESSAGES.requests.stepDone,
  ];
  const last = titles.length - 1;
  const current = (() => {
    switch (status) {
      case 'Completed':
        return last + 1;
      case 'Approved':
        return last;
      default:
        return 1;
    }
  })();
  const stopped = status === 'Rejected' || status === 'Cancelled';
  const stopTitle =
    status === 'Rejected' ? MESSAGES.requests.stepRejected : MESSAGES.requests.stepCancelled;

  return (
    <Steps
      size="small"
      current={Math.min(current, last)}
      status={stopped ? 'error' : current > last ? 'finish' : 'process'}
      items={titles.map((title, i) => ({
        title: stopped && i === Math.min(current, last) ? stopTitle : title,
      }))}
      style={{ marginBottom: 16 }}
    />
  );
}
