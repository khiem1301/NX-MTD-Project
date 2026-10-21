import { Button, Descriptions, Divider, Drawer, Result, Space, Spin, Table, Typography, message } from 'antd';
import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { AttachmentGallery } from '@/components/common/attachments';
import { NoteModal } from '@/components/common/NoteModal';
import { QueryError } from '@/components/common/QueryError';
import { StatusTag } from '@/components/common/StatusTag';
import { ERROR_CODES } from '@/constants/errorCodes';
import { MESSAGES } from '@/constants/messages';
import { REQUEST_TYPE_LABEL } from '@/constants/requestTypes';
import type { ApiError } from '@/types/api';
import { formatDate, formatDateTime, formatQty } from '@/utils/format';
import { DEFAULT_MAX_PHOTOS, GENERIC_FORM_TYPES, REQUEST_STATUS_TAG } from '../constants';
import { useRequestAction, useRequestDetail } from '../hooks/useRequests';
import type { RequestAction, RequestDetail } from '../types';
import { RequestFormModal } from './RequestFormModal';
import { RequestHistory } from './RequestHistory';

type ActionKind = RequestAction['kind'];

const PENDING_MODAL: Record<ActionKind, { title: string; label: string; required: boolean; danger: boolean; done: string }> = {
  approve: { title: MESSAGES.requests.approve, label: MESSAGES.requests.approveNote, required: false, danger: false, done: MESSAGES.requests.approved },
  reject: { title: MESSAGES.requests.reject, label: MESSAGES.requests.rejectReason, required: true, danger: true, done: MESSAGES.requests.rejected },
  complete: { title: MESSAGES.requests.complete, label: MESSAGES.requests.resultNote, required: false, danger: false, done: MESSAGES.requests.completed },
  cancel: { title: MESSAGES.requests.cancelRequest, label: MESSAGES.requests.cancelReason, required: false, danger: true, done: MESSAGES.requests.cancelled },
};

const buildAction = (kind: ActionKind, detail: RequestDetail, note: string | null): RequestAction => {
  const base = { id: detail.requestId, rowVer: detail.rowVer };
  switch (kind) {
    case 'approve':
      return { kind, ...base, note };
    case 'reject':
      return { kind, ...base, reason: note ?? '' };
    case 'complete':
      return { kind, ...base, resultNote: note };
    case 'cancel':
      return { kind, ...base, reason: note };
  }
};

/** Chi tiết đơn mở theo query ?id=<requestId> (link chia sẻ được). Nút hiện theo `permissions` server trả về. */
export function RequestDetailDrawer() {
  const [searchParams, setSearchParams] = useSearchParams();
  const idParam = Number(searchParams.get('id'));
  const id = Number.isInteger(idParam) && idParam > 0 ? idParam : null;
  const { data, isLoading, error, refetch } = useRequestDetail(id);
  const act = useRequestAction();
  const [pending, setPending] = useState<ActionKind | null>(null);
  const [editing, setEditing] = useState(false);

  const close = () => setSearchParams({});
  const notFound = (error as ApiError | null)?.errorCode === ERROR_CODES.CommonNotFound;

  const submit = (note: string | null) => {
    if (!pending || !data) return;
    const meta = PENDING_MODAL[pending];
    act.mutate(buildAction(pending, data, note), {
      onSuccess: () => void message.success(meta.done),
      onSettled: () => setPending(null),
    });
  };

  return (
    <Drawer open={id !== null} onClose={close} width={640} destroyOnHidden title={data?.requestCode} extra={data && <StatusTag status={data.status} labels={REQUEST_STATUS_TAG} />}>
      {isLoading && <Spin />}
      {notFound && <Result status="404" subTitle={MESSAGES.common.notFound} />}
      {!notFound && <QueryError error={error} onRetry={() => void refetch()} />}
      {data && (
        <>
          <Descriptions column={2} size="small" bordered>
            <Descriptions.Item label={MESSAGES.requests.type}>{REQUEST_TYPE_LABEL[data.requestType] ?? data.requestType}</Descriptions.Item>
            <Descriptions.Item label={MESSAGES.requests.createdBy}>{data.createdByName ?? '—'}</Descriptions.Item>
            <Descriptions.Item label={MESSAGES.requests.team}>{data.teamName ?? '—'}</Descriptions.Item>
            <Descriptions.Item label={MESSAGES.requests.createdAt}>{formatDateTime(data.createdAt)}</Descriptions.Item>
            <Descriptions.Item label={MESSAGES.requests.targetDate}>{formatDate(data.targetDate)}</Descriptions.Item>
            <Descriptions.Item label={MESSAGES.requests.order}>{data.orderCode ?? '—'}</Descriptions.Item>
            {data.decisionNote && (
              <Descriptions.Item label={MESSAGES.requests.decisionNote} span={2}>{data.decisionNote}</Descriptions.Item>
            )}
            {data.resultNote && (
              <Descriptions.Item label={MESSAGES.requests.result} span={2}>
                {`${data.resultNote} (${formatDateTime(data.completedAt)})`}
              </Descriptions.Item>
            )}
            {data.shortageResolution && (
              <Descriptions.Item label={MESSAGES.requests.shortageResolution} span={2}>{data.shortageResolution}</Descriptions.Item>
            )}
          </Descriptions>
          <Typography.Paragraph style={{ whiteSpace: 'pre-wrap', marginTop: 16 }}>{data.content}</Typography.Paragraph>
          {data.lines.length > 0 && (
            <Table
              size="small"
              rowKey="requestLineId"
              pagination={false}
              dataSource={data.lines}
              columns={[
                {
                  title: MESSAGES.requests.itemDescription,
                  render: (_: unknown, l) =>
                    [l.itemDescription, l.materialName && `${l.materialName}${l.materialColor ? ` (${l.materialColor})` : ''}`, l.orderLineLabel, l.panelName]
                      .filter(Boolean)
                      .join(' · ') || '—',
                },
                { title: MESSAGES.requests.quantity, dataIndex: 'quantity', align: 'right', render: formatQty },
              ]}
            />
          )}
          <Divider>{MESSAGES.attachments.title}</Divider>
          <AttachmentGallery ownerType="Request" ownerId={data.requestId} canEdit={data.permissions.canEdit} maxFiles={data.maxPhotos || DEFAULT_MAX_PHOTOS} />
          <Divider>{MESSAGES.common.history}</Divider>
          <RequestHistory createdByName={data.createdByName} createdAt={data.createdAt} history={data.history} />
          <Space style={{ marginTop: 16 }} wrap>
            {data.permissions.canEdit && (
              GENERIC_FORM_TYPES.includes(data.requestType) ? (
                <Button onClick={() => setEditing(true)}>{MESSAGES.requests.edit}</Button>
              ) : (
                <Typography.Text type="secondary">{MESSAGES.requests.otherFlowHint}</Typography.Text>
              )
            )}
            {data.permissions.canCancel && <Button danger onClick={() => setPending('cancel')}>{MESSAGES.requests.cancelRequest}</Button>}
            {data.permissions.canApprove && <Button type="primary" onClick={() => setPending('approve')}>{MESSAGES.requests.approve}</Button>}
            {data.permissions.canReject && <Button danger onClick={() => setPending('reject')}>{MESSAGES.requests.reject}</Button>}
            {data.permissions.canComplete && <Button type="primary" onClick={() => setPending('complete')}>{MESSAGES.requests.complete}</Button>}
          </Space>
          {pending && (
            <NoteModal
              open
              title={PENDING_MODAL[pending].title}
              label={PENDING_MODAL[pending].label}
              required={PENDING_MODAL[pending].required}
              danger={PENDING_MODAL[pending].danger}
              loading={act.isPending}
              onCancel={() => setPending(null)}
              onSubmit={submit}
            />
          )}
          <RequestFormModal
            open={editing}
            editing={data}
            maxPhotos={data.maxPhotos}
            onClose={() => setEditing(false)}
            onSaved={() => setEditing(false)}
          />
        </>
      )}
    </Drawer>
  );
}
