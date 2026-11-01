import { useFgHandover } from '../hooks/useFgHandover';
import { DetailModal } from '@/components/common/DetailModal';
import { FgHandoverModalBody } from './FgHandoverModalBody';

type FgHandoverModalProps = { handoverId: number | undefined; onClose: () => void };

/** Chi tiết phiếu bàn giao thành phẩm: Thủ kho đếm (lệch phải có lý do), nhận vào kho hoặc từ chối; QA/QC rút phiếu chờ. */
export function FgHandoverModal({ handoverId, onClose }: FgHandoverModalProps) {
  const detail = useFgHandover(handoverId);
  const h = detail.data;
  return (
    <DetailModal
      open={handoverId !== undefined}
      onClose={onClose}
      title={h?.handoverCode}
      loading={detail.isLoading}
      error={detail.error}
    >
      {h && <FgHandoverModalBody key={h.fgHandoverId} handover={h} onClose={onClose} />}
    </DetailModal>
  );
}
