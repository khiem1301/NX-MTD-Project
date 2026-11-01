import { Divider } from 'antd';
import { AttachmentGallery } from '@/components/common/attachments';
import { ROLES } from '@/constants/roles';
import { usePermission } from '@/hooks/usePermission';
import { ATTACHMENT_OWNER, ATTACHMENT_PURPOSE } from '@/types/attachment';
import { DISPATCH_LABELS, DISPATCH_PHOTO_MAX, DISPATCH_STATUS } from '../constants';
import type { DispatchNote } from '../types';

/** Ảnh chứng từ của phiếu xuất giao: phiếu xuất kho đã ký (thủ kho) và phiếu giao hàng đã ký (kế toán). */
export function DispatchPhotos({ note }: { note: DispatchNote }) {
  const { hasRole } = usePermission();
  const pending = note.status === DISPATCH_STATUS.PENDING;
  const inDelivery = note.status === DISPATCH_STATUS.IN_DELIVERY;
  const isKeeper = hasRole(ROLES.WarehouseKeeper);
  const isAccountant = hasRole(ROLES.Accountant);
  const isManager = hasRole(ROLES.Admin, ROLES.Manager);
  const sent =
    !pending &&
    note.status !== DISPATCH_STATUS.CANCELLED &&
    note.status !== DISPATCH_STATUS.REJECTED;
  return (
    <>
      {(pending || sent) && (isKeeper || isManager || sent) && (
        <>
          <Divider orientation="left">{`${DISPATCH_LABELS.issueSlipPhotos} (${DISPATCH_LABELS.requiredPhoto})`}</Divider>
          <AttachmentGallery
            ownerType={ATTACHMENT_OWNER.DispatchNote}
            ownerId={note.dispatchNoteId}
            canEdit={isKeeper && (pending || inDelivery)}
            maxFiles={DISPATCH_PHOTO_MAX}
            purpose={ATTACHMENT_PURPOSE.SignedIssueSlip}
            onlyPurpose
          />
        </>
      )}
      {sent && (
        <>
          <Divider orientation="left">{`${DISPATCH_LABELS.deliveryNotePhotos} (${DISPATCH_LABELS.requiredPhoto})`}</Divider>
          <AttachmentGallery
            ownerType={ATTACHMENT_OWNER.DispatchNote}
            ownerId={note.dispatchNoteId}
            canEdit={isAccountant && inDelivery}
            maxFiles={DISPATCH_PHOTO_MAX}
            purpose={ATTACHMENT_PURPOSE.SignedDeliveryNote}
            onlyPurpose
          />
        </>
      )}
    </>
  );
}
