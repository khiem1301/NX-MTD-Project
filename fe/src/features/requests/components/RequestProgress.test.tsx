import { render, screen } from '@testing-library/react';
import { MESSAGES } from '@/constants/messages';
import { RequestProgress } from './RequestProgress';

const stepTitles = () =>
  Array.from(document.querySelectorAll('.ant-steps-item-title')).map((e) => e.textContent);

describe('RequestProgress', () => {
  it('loại có duyệt: 3 bước, đang chờ duyệt ở bước Phê duyệt', () => {
    render(<RequestProgress requestType="Other" status="PendingApproval" />);

    expect(stepTitles()).toEqual([
      MESSAGES.requests.stepSent,
      MESSAGES.requests.stepApprove,
      MESSAGES.requests.stepDone,
    ]);
    expect(document.querySelector('.ant-steps-item-process')?.textContent).toContain(
      MESSAGES.requests.stepApprove,
    );
  });

  it('loại không duyệt: 2 bước', () => {
    render(<RequestProgress requestType="ShortDelivery" status="PendingProcessing" />);

    expect(stepTitles()).toEqual([MESSAGES.requests.stepSent, MESSAGES.requests.stepDone]);
  });

  it('bị từ chối: bước dừng đổi tên và báo lỗi', () => {
    render(<RequestProgress requestType="SpareParts" status="Rejected" />);

    expect(screen.getByText(MESSAGES.requests.stepRejected)).toBeInTheDocument();
    expect(document.querySelector('.ant-steps-item-error')).not.toBeNull();
  });

  it('không hoàn thành: bước cuối tô đỏ và đổi tên', () => {
    render(<RequestProgress requestType="MaterialRequisition" status="NotCompleted" />);

    expect(screen.getByText(MESSAGES.requests.stepNotCompleted)).toBeInTheDocument();
    expect(document.querySelector('.ant-steps-item-error')).not.toBeNull();
  });

  it('hoàn thành: mọi bước xong', () => {
    render(<RequestProgress requestType="SpareParts" status="Completed" />);

    expect(document.querySelectorAll('.ant-steps-item-finish')).toHaveLength(3);
  });
});
