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
} from 'antd';
import { PrinterOutlined } from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import { AttachmentGallery } from '@/components/common/attachments';
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
import {
  useCancelDispatch,
  useConfirmDelivery,
  useDispatchNote,
  useDownloadDispatch,
  useExportDispatch,
  useRejectDispatch,
} from '../hooks/useDispatch';
import type { DispatchNote, DispatchNoteLine } from '../types';

type DispatchDrawerProps = { noteId: number | undefined; onClose: () => void };
type FormValues = { receivedDate: Dayjs; received: Record<string, number> };

const API_DATE = 'YYYY-MM-DD';

/**
 * Chi tiết phiếu xuất hàng. Quản đốc hủy phiếu chưa xuất; Thủ kho xuất hàng (kèm ảnh phiếu xuất có Quản đốc ký) hoặc từ chối;
 * Kế toán xác nhận khách nhận (ngày nhận, số nhận từng dòng, ảnh biên bản giao có khách ký).
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
  const confirm = useConfirmDelivery();
  const download = useDownloadDispatch();
  const [rejecting, setRejecting] = useState(false);
  const [form] = Form.useForm<FormValues>();
  const pending = note.status === DISPATCH_STATUS.PENDING;
  const inDelivery = note.status === DISPATCH_STATUS.IN_DELIVERY;
  const isKeeper = hasRole(ROLES.WarehouseKeeper);
  const isAccountant = hasRole(ROLES.Accountant);
  const canExport = isKeeper && pending;
  const canConfirm = isAccountant && inDelivery;
  const sent =
    note.status !== DISPATCH_STATUS.PENDING &&
    note.status !== DISPATCH_STATUS.CANCELLED &&
    note.status !== DISPATCH_STATUS.REJECTED;

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
        {hasRole(ROLES.Manager) && pending && (
          <Popconfirm
            title={DISPATCH_MESSAGES.confirmCancel}
            onConfirm={() => cancel.mutate(note.dispatchNoteId, { onSuccess: onClose })}
          >
            <Button danger loading={cancel.isPending}>
              {DISPATCH_LABELS.cancel}
            </Button>
          </Popconfirm>
        )}
        {canExport && (
          <>
            <Button danger onClick={() => setRejecting(true)}>
              {DISPATCH_LABELS.reject}
            </Button>
            <Popconfirm
              title={DISPATCH_MESSAGES.confirmExport}
              onConfirm={() => exportGoods.mutate(note.dispatchNoteId, { onSuccess: onClose })}
            >
              <Button type="primary" loading={exportGoods.isPending}>
                {DISPATCH_LABELS.export}
              </Button>
            </Popconfirm>
          </>
        )}
        {canConfirm && (
          <Button type="primary" loading={confirm.isPending} onClick={() => form.submit()}>
            {DISPATCH_LABELS.confirmDelivery}
          </Button>
        )}
      </Space>
      {note.rejectReason && (
        <Alert
          type="warning"
          showIcon
          message={`${DISPATCH_LABELS.rejectReason}: ${note.rejectReason}`}
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
      {(isKeeper || hasRole(ROLES.Manager) || sent) && (
        <>
          <Divider orientation="left">{DISPATCH_LABELS.issueSlipPhotos}</Divider>
          <AttachmentGallery
            ownerType={ATTACHMENT_OWNER.DispatchNote}
            ownerId={note.dispatchNoteId}
            canEdit={isKeeper && (pending || inDelivery)}
            maxFiles={DISPATCH_PHOTO_MAX}
            purpose={ATTACHMENT_PURPOSE.SignedIssueSlip}
          />
        </>
      )}
      {sent && (
        <>
          <Divider orientation="left">{DISPATCH_LABELS.deliveryNotePhotos}</Divider>
          <AttachmentGallery
            ownerType={ATTACHMENT_OWNER.DispatchNote}
            ownerId={note.dispatchNoteId}
            canEdit={isAccountant}
            maxFiles={DISPATCH_PHOTO_MAX}
            purpose={ATTACHMENT_PURPOSE.SignedDeliveryNote}
          />
        </>
      )}
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
