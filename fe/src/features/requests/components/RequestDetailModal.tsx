import { Button, Descriptions, Divider, Result, Spin, Typography } from 'antd';
import { AttachmentGallery } from '@/components/common/attachments';
import { NoteModal } from '@/components/common/NoteModal';
import { QueryError } from '@/components/common/QueryError';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { REQUEST_TYPE_LABEL } from '@/constants/requestTypes';
import { ROLE_LABEL } from '@/constants/hr';
import { fill, formatDate, formatDateTime, formatQty } from '@/utils/format';
import { RequestProgress } from './RequestProgress';
import { DEFAULT_MAX_PHOTOS, GENERIC_FORM_TYPES, REQUEST_STATUS_TAG } from '../constants';
import { RequestFormModal } from './RequestFormModal';
import { RequestHistory } from './RequestHistory';
import { DetailModal, ModalActions } from '@/components/common/DetailModal';
import { DataTable } from '@/components/common/DataTable';
import { PENDING_MODAL, useRequestDetailModal } from '../hooks/useRequestDetailModal';

/** Chi tiết đơn mở theo query ?id=<requestId> (link chia sẻ được). Nút hiện theo `permissions` server trả về. */
export function RequestDetailModal() {
  const {
    id,
    act,
    close,
    notFound,
    submit,
    pending,
    setPending,
    editing,
    setEditing,
    data,
    isLoading,
    error,
    refetch,
  } = useRequestDetailModal();

  return (
    <DetailModal
      open={id !== null}
      onClose={close}
      title={data?.requestCode}
      extra={data && <StatusTag status={data.status} labels={REQUEST_STATUS_TAG} />}
    >
      {isLoading && <Spin />}
      {notFound && <Result status="404" subTitle={MESSAGES.common.notFound} />}
      {!notFound && <QueryError error={error} onRetry={() => void refetch()} />}
      {data && (
        <>
          <RequestProgress requestType={data.requestType} status={data.status} />
          <Descriptions column={{ xs: 1, sm: 2 }} size="small" bordered>
            <Descriptions.Item label={MESSAGES.requests.type}>
              {REQUEST_TYPE_LABEL[data.requestType] ?? data.requestType}
            </Descriptions.Item>
            <Descriptions.Item label={MESSAGES.requests.createdBy}>
              {data.createdByName ?? '—'}
            </Descriptions.Item>
            <Descriptions.Item label={MESSAGES.requests.team}>
              {data.teamName ?? '—'}
            </Descriptions.Item>
            <Descriptions.Item label={MESSAGES.requests.createdAt}>
              {formatDateTime(data.createdAt)}
            </Descriptions.Item>
            <Descriptions.Item label={MESSAGES.requests.targetDate}>
              {formatDate(data.targetDate)}
            </Descriptions.Item>
            <Descriptions.Item label={MESSAGES.requests.order}>
              {data.orderCode ?? '—'}
            </Descriptions.Item>
            {data.workOrderCode && (
              <Descriptions.Item label={MESSAGES.requests.workOrder}>
                {data.workOrderCode}
              </Descriptions.Item>
            )}
            {data.handlerRole && (
              <Descriptions.Item label={MESSAGES.requests.handlerRole} span={2}>
                {data.handlerTeamName
                  ? fill(
                      MESSAGES.requests.handlerOfTeam,
                      ROLE_LABEL[data.handlerRole],
                      data.handlerTeamName,
                    )
                  : ROLE_LABEL[data.handlerRole]}
              </Descriptions.Item>
            )}
            {data.decisionNote && (
              <Descriptions.Item label={MESSAGES.requests.decisionNote} span={2}>
                {data.decisionNote}
              </Descriptions.Item>
            )}
            {data.resultNote && (
              <Descriptions.Item
                label={
                  data.status === 'NotCompleted'
                    ? MESSAGES.requests.notCompleteReason
                    : MESSAGES.requests.result
                }
                span={2}
              >
                {`${data.resultNote} (${formatDateTime(data.completedAt)})`}
              </Descriptions.Item>
            )}
            {data.shortageResolution && (
              <Descriptions.Item label={MESSAGES.requests.shortageResolution} span={2}>
                {data.shortageResolution}
              </Descriptions.Item>
            )}
          </Descriptions>
          <Typography.Paragraph style={{ whiteSpace: 'pre-wrap', marginTop: 16 }}>
            {data.content}
          </Typography.Paragraph>
          {data.lines.length > 0 && (
            <DataTable
              rowKey="requestLineId"
              pagination={false}
              dataSource={data.lines}
              columns={[
                {
                  title: MESSAGES.requests.itemDescription,
                  render: (_: unknown, l) =>
                    [
                      l.itemDescription,
                      l.materialName &&
                        `${l.materialName}${l.materialColor ? ` (${l.materialColor})` : ''}`,
                      l.orderLineLabel,
                      l.componentName,
                    ]
                      .filter(Boolean)
                      .join(' · ') || '—',
                },
                {
                  title: MESSAGES.requests.quantity,
                  dataIndex: 'quantity',
                  align: 'right',
                  render: formatQty,
                },
              ]}
            />
          )}
          <Divider>{MESSAGES.attachments.title}</Divider>
          <AttachmentGallery
            ownerType="Request"
            ownerId={data.requestId}
            canEdit={data.permissions.canEdit}
            maxFiles={data.maxPhotos || DEFAULT_MAX_PHOTOS}
          />
          <Divider>{MESSAGES.common.history}</Divider>
          <RequestHistory
            createdByName={data.createdByName}
            createdAt={data.createdAt}
            history={data.history}
          />
          <ModalActions>
            {data.permissions.canEdit &&
              (GENERIC_FORM_TYPES.includes(data.requestType) ? (
                <Button onClick={() => setEditing(true)}>{MESSAGES.requests.edit}</Button>
              ) : (
                <Typography.Text type="secondary">
                  {MESSAGES.requests.otherFlowHint}
                </Typography.Text>
              ))}
            {data.permissions.canCancel && (
              <Button danger onClick={() => setPending('cancel')}>
                {MESSAGES.requests.cancelRequest}
              </Button>
            )}
            {data.permissions.canReject && (
              <Button danger onClick={() => setPending('reject')}>
                {MESSAGES.requests.reject}
              </Button>
            )}
            {data.permissions.canApprove && (
              <Button type="primary" onClick={() => setPending('approve')}>
                {MESSAGES.requests.approve}
              </Button>
            )}
            {data.permissions.canNotComplete && (
              <Button danger onClick={() => setPending('notComplete')}>
                {MESSAGES.requests.notComplete}
              </Button>
            )}
            {data.permissions.canComplete && (
              <Button type="primary" onClick={() => setPending('complete')}>
                {MESSAGES.requests.complete}
              </Button>
            )}
          </ModalActions>
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
    </DetailModal>
  );
}
