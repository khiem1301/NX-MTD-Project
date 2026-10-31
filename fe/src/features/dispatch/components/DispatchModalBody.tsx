import { useState } from 'react';
import { Alert, Button, Form, Popconfirm, Tooltip } from 'antd';
import { PrinterOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { useAttachments } from '@/hooks/useAttachments';
import { ReasonModal } from '@/components/common/ReasonModal';
import { MESSAGES } from '@/constants/messages';
import { ROLES } from '@/constants/roles';
import { usePermission } from '@/hooks/usePermission';
import { ATTACHMENT_OWNER, ATTACHMENT_PURPOSE } from '@/types/attachment';
import { API_DATE } from '@/utils/format';
import { DISPATCH_LABELS, DISPATCH_MESSAGES, DISPATCH_STATUS } from '../constants';
import {
  useCancelDispatch,
  useConfirmDelivery,
  useDownloadDispatch,
  useExportDispatch,
  useRejectDispatch,
} from '../hooks/useDispatch';
import type { DispatchNote, DispatchNoteLine } from '../types';
import { ModalActions } from '@/components/common/DetailModal';
import { DataTable } from '@/components/common/DataTable';
import { AppDatePicker } from '@/components/common/DateInputs';
import { type Dayjs } from 'dayjs';
import { DispatchInfo } from './DispatchInfo';
import { DispatchPhotos } from './DispatchPhotos';
import { dispatchLineColumns } from './dispatchLineColumns';

type FormValues = { receivedDate: Dayjs; received: Record<string, number> };

export function DispatchModalBody({ note, onClose }: { note: DispatchNote; onClose: () => void }) {
  const { hasRole } = usePermission();
  const cancel = useCancelDispatch();
  const exportGoods = useExportDispatch();
  const reject = useRejectDispatch();
  const photos = useAttachments(ATTACHMENT_OWNER.DispatchNote, note.dispatchNoteId);
  const hasIssuePhoto = (photos.data ?? []).some(
    (a) => a.purpose === ATTACHMENT_PURPOSE.SignedIssueSlip,
  );
  const hasDeliveryPhoto = (photos.data ?? []).some(
    (a) => a.purpose === ATTACHMENT_PURPOSE.SignedDeliveryNote,
  );
  const confirm = useConfirmDelivery();
  const download = useDownloadDispatch();
  const [rejecting, setRejecting] = useState(false);
  const [form] = Form.useForm<FormValues>();
  const pending = note.status === DISPATCH_STATUS.PENDING;
  const inDelivery = note.status === DISPATCH_STATUS.IN_DELIVERY;
  const isKeeper = hasRole(ROLES.WarehouseKeeper);
  const isAccountant = hasRole(ROLES.Accountant);
  const isManager = hasRole(ROLES.Manager);
  const canExport = isKeeper && pending;
  const canConfirm = isAccountant && inDelivery;
  const notExportedYet = pending;

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
      <ModalActions>
        <Button
          icon={<PrinterOutlined />}
          loading={download.isPending}
          onClick={() => download.mutate({ id: note.dispatchNoteId, code: note.dispatchCode })}
        >
          {MESSAGES.common.printExcel}
        </Button>
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
          <Button danger onClick={() => setRejecting(true)}>
            {DISPATCH_LABELS.reject}
          </Button>
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
              <Button
                type="primary"
                disabled={!hasDeliveryPhoto}
                loading={confirm.isPending}
                onClick={() => form.submit()}
              >
                {DISPATCH_LABELS.confirmDelivery}
              </Button>
            </span>
          </Tooltip>
        )}
      </ModalActions>
      {note.rejectReason && (
        <Alert
          type="warning"
          showIcon
          message={`${DISPATCH_LABELS.rejectReason}: ${note.rejectReason}`}
          style={{ marginBottom: 16 }}
        />
      )}
      <DispatchInfo note={note} />
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
            <AppDatePicker />
          </Form.Item>
        )}
        <DataTable<DispatchNoteLine>
          rowKey="dispatchNoteLineId"
          size="small"
          pagination={false}
          dataSource={note.lines}
          columns={dispatchLineColumns(canConfirm)}
        />
      </Form>
      <DispatchPhotos note={note} />
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
