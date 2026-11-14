import { Modal, Spin } from 'antd';
import { QueryError } from '@/components/common/QueryError';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { KPI_TEXT } from '../constants';
import { useMismatchDetail } from '../hooks/useKpi';
import { MismatchDetail } from './MismatchDetail';

const text = KPI_TEXT.finish;

type MismatchDetailModalProps = {
  /** Quản đốc / Admin dùng endpoint xem trước, tổ trưởng / tổ phó dùng endpoint chỉ xem của tổ mình. */
  role: 'manager' | 'team-leader';
  /** Id chỉ tiêu KPI cần xem; null thì đóng. */
  kpiTargetId: number | null;
  onClose: () => void;
};

/**
 * Chi tiết lệch kiểm cuối đơn của một chỉ tiêu, chỉ xem. Mở NGAY TẠI CHỖ khi bấm thông báo lệch (chuông, toast, hộp thư) mà không chuyển
 * trang. Dữ liệu tính lại mỗi lần mở nên phản ánh hiện tại (lệch đã xử lý bằng phiếu lỗi thì giảm).
 */
export function MismatchDetailModal({ role, kpiTargetId, onClose }: MismatchDetailModalProps) {
  const detail = useMismatchDetail(role, kpiTargetId);
  const data = detail.data;

  return (
    <Modal
      open={kpiTargetId !== null}
      width={MODAL_WIDTH.md}
      title={data ? `${text.detailTitle}: ${data.orderCode} · ${data.teamName}` : text.detailTitle}
      footer={null}
      onCancel={onClose}
      destroyOnHidden
    >
      {detail.isLoading && <Spin />}
      <QueryError error={detail.error} onRetry={() => void detail.refetch()} />
      {data && <MismatchDetail operations={data.operations} emptyText={text.detailEmpty} />}
    </Modal>
  );
}
