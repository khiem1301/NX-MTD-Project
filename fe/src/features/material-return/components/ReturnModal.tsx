import { useMaterialReturn } from '../hooks/useMaterialReturn';
import { DetailModal } from '@/components/common/DetailModal';
import { ReturnModalBody } from './ReturnModalBody';

type ReturnModalProps = { returnId: number | undefined; onClose: () => void };

/** Chi tiết phiếu trả vải: Thủ kho đếm (lệch phải có lý do), nhận vào kho hoặc từ chối; tổ cắt rút phiếu chờ. */
export function ReturnModal({ returnId, onClose }: ReturnModalProps) {
  const detail = useMaterialReturn(returnId);
  const r = detail.data;
  return (
    <DetailModal
      open={returnId !== undefined}
      onClose={onClose}
      title={r?.returnCode}
      loading={detail.isLoading}
      error={detail.error}
    >
      {r && <ReturnModalBody key={r.materialReturnId} ret={r} onClose={onClose} />}
    </DetailModal>
  );
}
