import { useState } from 'react';
import {
  Alert,
  Button,
  DatePicker,
  Descriptions,
  Divider,
  Drawer,
  Form,
  InputNumber,
  Popconfirm,
  Space,
  Spin,
  Table,
  Tooltip,
} from 'antd';
import { PrinterOutlined } from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import { AttachmentGallery } from '@/components/common/attachments';
import { useAttachments } from '@/hooks/useAttachments';
import { ReasonModal } from '@/components/common/ReasonModal';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { ROLES } from '@/constants/roles';
import { usePermission } from '@/hooks/usePermission';
import { ATTACHMENT_OWNER, ATTACHMENT_PURPOSE } from '@/types/attachment';
import { formatDate, formatDateTime } from '@/utils/format';
import {
  DISPATCH_LABELS,
  DISPATCH_MESSAGES,
  DISPATCH_PHOTO_MAX,
  DISPATCH_STATUS,
  DISPATCH_STATUS_LABEL,
} from '../constants';
import { PrepareDispatchModal } from './PrepareDispatchModal';
import {
  useApproveDispatch,
  useCancelDispatch,
  useConfirmDelivery,
  useDispatchNote,
  useDownloadDispatch,
  useExportDispatch,
  useRejectDispatch,
  useReturnDispatch,
} from '../hooks/useDispatch';
import type { DispatchNote, DispatchNoteLine } from '../types';

type DispatchDrawerProps = { noteId: number | undefined; onClose: () => void };
type FormValues = { receivedDate: Dayjs; received: Record<string, number> };

const API_DATE = 'YYYY-MM-DD';

/**
 * Chi tiết phiếu xuất hàng. Quản đốc lập → Thủ kho lập phiếu xuất (chốt số xuất) → Quản đốc duyệt hoặc trả lại → Thủ kho in,
 * chụp ảnh phiếu có ký (bắt buộc) rồi xuất; Quản đốc hủy được đến khi hàng rời kho; Kế toán xác nhận khách nhận
 * (ngày nhận, số nhận từng dòng, ảnh biên bản giao có khách ký — bắt buộc).
 */
export function DispatchDrawer({ noteId, onClose }: DispatchDrawerProps) {
  const detail = useDispatchNote(noteId);
  const note = detail.data;
  return (
    <Drawer
      open={noteId !== undefined}
      onClose={onClose}
      width={860}
      title={note?.dispatchCode}
      destroyOnHidden
    >
      {detail.isLoading && <Spin />}
      {detail.error && <Alert type="error" message={detail.error.message} />}
      {note && <DrawerBody key={note.dispatchNoteId} note={note} onClose={onClose} />}
    </Drawer>
  );
}

function DrawerBody({ note, onClose }: { note: DispatchNote; onClose: () => void }) {
  const { hasRole } = usePermission();
  const cancel = useCancelDispatch();
  const exportGoods = useExportDispatch();
  const reject = useRejectDispatch();
  const approve = useApproveDispatch();
  const giveBack = useReturnDispatch();
  const photos = useAttachments(ATTACHMENT_OWNER.DispatchNote, note.dispatchNoteId);
  const hasIssuePhoto = (photos.data ?? []).some((a) => a.purpose === ATTACHMENT_PURPOSE.SignedIssueSlip);
  const hasDeliveryPhoto = (photos.data ?? []).some((a) => a.purpose === ATTACHMENT_PURPOSE.SignedDeliveryNote);
  const [preparing, setPreparing] = useState(false);
  const [returning, setReturning] = useState(false);
  const confirm = useConfirmDelivery();
  const download = useDownloadDispatch();
  const [rejecting, setRejecting] = useState(false);
  const [form] = Form.useForm<FormValues>();
  const pending = note.status === DISPATCH_STATUS.PENDING;
  const waitingApproval = note.status === DISPATCH_STATUS.PENDING_APPROVAL;
  const approved = note.status === DISPATCH_STATUS.APPROVED;
  const inDelivery = note.status === DISPATCH_STATUS.IN_DELIVERY;
  const isKeeper = hasRole(ROLES.WarehouseKeeper);
  const isAccountant = hasRole(ROLES.Accountant);
  const isManager = hasRole(ROLES.Manager);
  const canExport = isKeeper && approved;
  const canConfirm = isAccountant && inDelivery;
  const notExportedYet = pending || waitingApproval || approved;
  const sent = !notExportedYet && note.status !== DISPATCH_STATUS.CANCELLED && note.status !== DISPATCH_STATUS.REJECTED;

  const handleFinish = (values: FormValues) =>
    confirm.mutate(
      {
        id: note.dispatchNoteId,
        body: {
          customerReceivedDate: values.receivedDate.format(API_DATE),
          lines: note.lines.map((l) => ({
            dispatchNoteLineId: l.dispatchNoteLineId,
            receivedQty: values.received[String(l.dispatchNoteLineId)] ?? l.issueQty,
          })),
        },
      },
      { onSuccess: onClose },
    );

  return (
    <>
      <Space style={{ marginBottom: 16 }}>
        <Button
          icon={<PrinterOutlined />}
          loading={download.isPending}
          onClick={() => download.mutate({ id: note.dispatchNoteId, code: note.dispatchCode })}
        >
          {MESSAGES.common.printExcel}
        </Button>
        {isManager && waitingApproval && (
          <>
            <Button danger onClick={() => setReturning(true)}>
              {DISPATCH_LABELS.returnBack}
            </Button>
            <Popconfirm title={DISPATCH_MESSAGES.confirmApprove} onConfirm={() => approve.mutate(note.dispatchNoteId, { onSuccess: onClose })}>
              <Button type="primary" loading={approve.isPending}>
                {DISPATCH_LABELS.approve}
              </Button>
            </Popconfirm>
          </>
        )}
        {isManager && notExportedYet && (
          <Popconfirm
            title={DISPATCH_MESSAGES.confirmCancel}
            onConfirm={() => cancel.mutate(note.dispatchNoteId, { onSuccess: onClose })}
          >
            <Button danger loading={cancel.isPending}>
              {DISPATCH_LABELS.cancel}
            </Button>
          </Popconfirm>
        )}
        {isKeeper && pending && (
          <>
            <Button danger onClick={() => setRejecting(true)}>
              {DISPATCH_LABELS.reject}
            </Button>
            <Button type="primary" onClick={() => setPreparing(true)}>
              {DISPATCH_LABELS.prepare}
            </Button>
          </>
        )}
        {canExport && (
          <Tooltip title={hasIssuePhoto ? undefined : DISPATCH_LABELS.needIssuePhoto}>
            <span>
              <Popconfirm
                title={DISPATCH_MESSAGES.confirmExport}
                disabled={!hasIssuePhoto}
                onConfirm={() => exportGoods.mutate(note.dispatchNoteId, { onSuccess: onClose })}
              >
                <Button type="primary" disabled={!hasIssuePhoto} loading={exportGoods.isPending}>
                  {DISPATCH_LABELS.export}
                </Button>
              </Popconfirm>
            </span>
          </Tooltip>
        )}
        {canConfirm && (
          <Tooltip title={hasDeliveryPhoto ? undefined : DISPATCH_LABELS.needDeliveryPhoto}>
            <span>
              <Button type="primary" disabled={!hasDeliveryPhoto} loading={confirm.isPending} onClick={() => form.submit()}>
                {DISPATCH_LABELS.confirmDelivery}
              </Button>
            </span>
          </Tooltip>
        )}
      </Space>
      {note.rejectReason && (
        <Alert
          type="warning"
          showIcon
          // Phiếu đang chờ làm lại: lý do là của Quản đốc khi trả lại ("Quản đốc trả lại: ...").
          message={pending ? note.rejectReason : `${DISPATCH_LABELS.rejectReason}: ${note.rejectReason}`}
          style={{ marginBottom: 16 }}
        />
      )}
      <Descriptions column={2} size="small" bordered>
        <Descriptions.Item label={DISPATCH_LABELS.order}>{note.orderCode}</Descriptions.Item>
        <Descriptions.Item label={DISPATCH_LABELS.customer}>{note.customerName}</Descriptions.Item>
        <Descriptions.Item label={DISPATCH_LABELS.deliveryDate}>
          {formatDate(note.deliveryDate)}
        </Descriptions.Item>
        <Descriptions.Item label={DISPATCH_LABELS.status}>
          <StatusTag status={note.status} labels={DISPATCH_STATUS_LABEL} />
        </Descriptions.Item>
        <Descriptions.Item label={DISPATCH_LABELS.deliveryAddress} span={2}>
          {note.deliveryAddress}
        </Descriptions.Item>
        <Descriptions.Item label={DISPATCH_LABELS.dispatchedBy}>
          {note.dispatchedByName
            ? `${note.dispatchedByName} · ${formatDateTime(note.dispatchedAt)}`
            : '—'}
        </Descriptions.Item>
        <Descriptions.Item label={DISPATCH_LABELS.customerReceivedDate}>
          {note.customerReceivedDate ? formatDate(note.customerReceivedDate) : '—'}
        </Descriptions.Item>
        <Descriptions.Item label={DISPATCH_LABELS.note} span={2}>
          {note.note ?? '—'}
        </Descriptions.Item>
      </Descriptions>
      <Form
        form={form}
        disabled={confirm.isPending}
        style={{ marginTop: 16 }}
        initialValues={{
          receivedDate: dayjs(),
          received: Object.fromEntries(
            note.lines.map((l) => [String(l.dispatchNoteLineId), l.issueQty]),
          ),
        }}
        onFinish={handleFinish}
      >
        {canConfirm && (
          <Form.Item
            name="receivedDate"
            label={DISPATCH_LABELS.customerReceivedDate}
            rules={[{ required: true, message: MESSAGES.common.required }]}
          >
            <DatePicker format="DD/MM/YYYY" />
          </Form.Item>
        )}
        <Table<DispatchNoteLine>
          rowKey="dispatchNoteLineId"
          size="small"
          pagination={false}
          dataSource={note.lines}
          columns={[
            {
              title: DISPATCH_LABELS.orderLine,
              key: 'line',
              render: (_, l) => `${l.productCode} - ${l.color} - ${l.sizeCode}`,
            },
            { title: DISPATCH_LABELS.issueQty, dataIndex: 'issueQty', align: 'right' },
            {
              title: DISPATCH_LABELS.receivedQty,
              key: 'received',
              align: 'right',
              render: (_, l) =>
                canConfirm ? (
                  <Form.Item
                    name={['received', String(l.dispatchNoteLineId)]}
                    noStyle
                    rules={[{ required: true, message: MESSAGES.common.required }]}
                  >
                    <InputNumber min={0} max={l.issueQty} precision={0} />
                  </Form.Item>
                ) : (
                  (l.receivedQty ?? '—')
                ),
            },
          ]}
        />
      </Form>
      {(approved || sent) && (isKeeper || isManager || sent) && (
        <>
          <Divider orientation="left">{`${DISPATCH_LABELS.issueSlipPhotos} (${DISPATCH_LABELS.requiredPhoto})`}</Divider>
          <AttachmentGallery
            ownerType={ATTACHMENT_OWNER.DispatchNote}
            ownerId={note.dispatchNoteId}
            canEdit={isKeeper && (approved || inDelivery)}
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
      <PrepareDispatchModal note={note} open={preparing} onClose={() => setPreparing(false)} />
      <ReasonModal
        open={returning}
        title={DISPATCH_LABELS.returnTitle}
        loading={giveBack.isPending}
        onCancel={() => setReturning(false)}
        onSubmit={(reason) =>
          giveBack.mutate({ id: note.dispatchNoteId, reason }, { onSuccess: () => { setReturning(false); onClose(); } })
        }
      />
      <ReasonModal
        open={rejecting}
        title={DISPATCH_LABELS.rejectTitle}
        loading={reject.isPending}
        onCancel={() => setRejecting(false)}
        onSubmit={(reason) =>
          reject.mutate(
            { id: note.dispatchNoteId, reason },
            {
              onSuccess: () => {
                setRejecting(false);
                onClose();
              },
            },
          )
        }
      />
    </>
  );
}
