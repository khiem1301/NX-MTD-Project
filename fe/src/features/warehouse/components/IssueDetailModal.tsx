import { useEffect, useState } from 'react';
import { PrinterOutlined } from '@ant-design/icons';
import { Alert, Button, Descriptions, Form, Popconfirm, Space } from 'antd';
import { ReasonModal } from '@/components/common/ReasonModal';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { ROLES } from '@/constants/roles';
import { usePermission } from '@/hooks/usePermission';
import { formatDate, formatDateTime } from '@/utils/format';
import { ISSUE_STATUS, ISSUE_STATUS_LABEL, LABELS, WAREHOUSE_TYPE_LABEL } from '../constants';
import { useConfirmIssue, useDownloadIssue, useIssue, useRejectIssue } from '../hooks/useIssues';
import type { ConfirmIssuePayload } from '../types';
import { IssueLinesTable } from './IssueLinesTable';
import { DetailModal, DetailDescriptions } from '@/components/common/DetailModal';

type IssueDetailModalProps = { issueId: number | undefined; onClose: () => void };

/** Chi tiết phiếu cấp; tổ trưởng / tổ phó tổ nhận đếm và xác nhận số thực nhận hoặc từ chối (UC41). */
export function IssueDetailModal({ issueId, onClose }: IssueDetailModalProps) {
  const { hasRole } = usePermission();
  const issue = useIssue(issueId);
  const confirm = useConfirmIssue();
  const reject = useRejectIssue();
  const download = useDownloadIssue();
  const [rejecting, setRejecting] = useState(false);
  const [form] = Form.useForm<ConfirmIssuePayload>();
  const i = issue.data;
  const canReceive =
    hasRole(ROLES.TeamLeader, ROLES.TeamDeputy) && i?.status === ISSUE_STATUS.PENDING_RECEIPT;

  useEffect(() => {
    if (!i) return;
    form.setFieldsValue({
      lines: i.lines.map((l) => ({
        materialIssueLineId: l.materialIssueLineId,
        confirmedQty: l.issuedQty,
      })),
    });
  }, [i, form]);

  const handleConfirm = (values: ConfirmIssuePayload) => {
    if (!i) return;
    confirm.mutate({ id: i.materialIssueId, body: values }, { onSuccess: onClose });
  };

  const actions = i && (
    <Space>
      <Button
        icon={<PrinterOutlined />}
        loading={download.isPending}
        onClick={() => download.mutate({ id: i.materialIssueId, code: i.issueCode })}
      >
        {MESSAGES.common.printExcel}
      </Button>
      {canReceive && (
        <>
          <Button danger onClick={() => setRejecting(true)}>
            {LABELS.reject}
          </Button>
          <Popconfirm title={MESSAGES.warehouse.confirmIssue} onConfirm={() => form.submit()}>
            <Button type="primary" loading={confirm.isPending}>
              {LABELS.receive}
            </Button>
          </Popconfirm>
        </>
      )}
    </Space>
  );

  return (
    <DetailModal
      open={issueId !== undefined}
      onClose={onClose}
      title={i?.issueCode}
      extra={actions}
      loading={issue.isLoading}
      error={issue.error}
    >
      {i && (
        <>
          {i.rejectReason && (
            <Alert type="warning" showIcon message={i.rejectReason} style={{ marginBottom: 16 }} />
          )}
          <DetailDescriptions>
            <Descriptions.Item label={LABELS.warehouseType}>
              {WAREHOUSE_TYPE_LABEL[i.warehouseType]}
            </Descriptions.Item>
            <Descriptions.Item label={LABELS.status}>
              <StatusTag status={i.status} labels={ISSUE_STATUS_LABEL} />
            </Descriptions.Item>
            <Descriptions.Item label={LABELS.order}>{i.orderCode}</Descriptions.Item>
            <Descriptions.Item label={LABELS.workOrder}>{i.workOrderCode ?? '—'}</Descriptions.Item>
            <Descriptions.Item label={LABELS.receivingTeam}>
              {i.receivingTeamName}
            </Descriptions.Item>
            <Descriptions.Item label={LABELS.issueDate}>
              {formatDate(i.issueDate)}
            </Descriptions.Item>
            <Descriptions.Item label={LABELS.issuedBy}>{i.issuedByName}</Descriptions.Item>
            <Descriptions.Item label={LABELS.receive}>
              {i.receivedByName ? `${i.receivedByName} · ${formatDateTime(i.receivedAt)}` : '—'}
            </Descriptions.Item>
          </DetailDescriptions>
          <Form
            form={form}
            onFinish={handleConfirm}
            disabled={confirm.isPending}
            style={{ marginTop: 16 }}
          >
            <IssueLinesTable lines={i.lines} editable={canReceive} />
          </Form>
          <ReasonModal
            open={rejecting}
            title={LABELS.rejectTitle}
            loading={reject.isPending}
            onCancel={() => setRejecting(false)}
            onSubmit={(reason) =>
              reject.mutate(
                { id: i.materialIssueId, reason },
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
      )}
    </DetailModal>
  );
}
