import { useDispatchNote } from '../hooks/useDispatch';
import { DetailModal } from '@/components/common/DetailModal';
import { DispatchModalBody } from './DispatchModalBody';

type DispatchModalProps = { noteId: number | undefined; onClose: () => void };

/**
 * Chi tiết phiếu xuất hàng. Quản đốc lập → Thủ kho in, chụp ảnh phiếu có Quản đốc ký (bắt buộc) rồi xuất hoặc từ chối;
 * Quản đốc hủy được đến khi hàng rời kho; Kế toán xác nhận khách nhận
 * (ngày nhận, số nhận từng dòng, ảnh biên bản giao có khách ký — bắt buộc).
 */
export function DispatchModal({ noteId, onClose }: DispatchModalProps) {
  const detail = useDispatchNote(noteId);
  const note = detail.data;
  return (
    <DetailModal
      open={noteId !== undefined}
      onClose={onClose}
      title={note?.dispatchCode}
      loading={detail.isLoading}
      error={detail.error}
    >
      {note && <DispatchModalBody key={note.dispatchNoteId} note={note} onClose={onClose} />}
    </DetailModal>
  );
}
