import { useState } from 'react';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { Alert, Button, Descriptions, Modal, Popconfirm, Space, Spin, Tag } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { DownloadOutlined } from '@ant-design/icons';
import { ReasonModal } from '@/components/common/ReasonModal';
import { MESSAGES } from '@/constants/messages';
import { formatDateTime, formatMonth } from '@/utils/format';
import { PAYROLL_STATUS_LABEL, PAYROLL_TEXT } from '../constants';
import {
  useApprovePayroll,
  useDownloadImportedFile,
  usePayrollDetail,
  useRejectPayroll,
  useReopenPayroll,
} from '../hooks/usePayroll';
import type { Payslip } from '../types';
import { formatCurrency } from '../utils';
import { PayslipItemsView } from './PayslipItemsView';
import { DetailModal } from '@/components/common/DetailModal';
import { DataTable } from '@/components/common/DataTable';

type Props = { payrollId: number | null; onClose: () => void };

/** UC32 — Admin xem chi tiết bảng lương (từng phiếu lương), duyệt (khóa tháng), từ chối hoặc mở lại. */
export function PayrollDetailModal({ payrollId, onClose }: Props) {
  const detail = usePayrollDetail(payrollId);
  const approve = useApprovePayroll(payrollId ?? 0);
  const reject = useRejectPayroll(payrollId ?? 0);
  const reopen = useReopenPayroll(payrollId ?? 0);
  const downloadFile = useDownloadImportedFile('admin');
  const [rejecting, setRejecting] = useState(false);
  const [reopening, setReopening] = useState(false);
  const [viewingPayslip, setViewingPayslip] = useState<Payslip | null>(null);

  const d = detail.data;
  const canApprove = d?.status === 'PendingApproval';
  const canReject = d?.status === 'PendingApproval';
  const canReopen = d?.status === 'Approved';

  const columns: ColumnsType<Payslip> = [
    {
      title: PAYROLL_TEXT.table.employee,
      dataIndex: 'fullName',
      render: (v: string, row) => `${row.employeeCode} - ${v}`,
    },
    {
      title: PAYROLL_TEXT.table.team,
      dataIndex: 'teamName',
      render: (v: string | null) => v ?? '—',
    },
    {
      title: PAYROLL_TEXT.table.workDays,
      dataIndex: 'workDays',
      align: 'right',
      render: (v: number | null) => v ?? '—',
    },
    {
      title: PAYROLL_TEXT.table.totalAdditions,
      dataIndex: 'totalAdditions',
      align: 'right',
      render: (v: number) => formatCurrency(v),
    },
    {
      title: PAYROLL_TEXT.table.totalDeductions,
      dataIndex: 'totalDeductions',
      align: 'right',
      render: (v: number) => formatCurrency(v),
    },
    {
      title: PAYROLL_TEXT.table.insurance,
      dataIndex: 'insurance',
      align: 'right',
      render: (v: number) => formatCurrency(v),
    },
    {
      title: PAYROLL_TEXT.table.netPay,
      dataIndex: 'netPay',
      align: 'right',
      render: (v: number) => formatCurrency(v),
    },
    {
      title: MESSAGES.common.actions,
      key: 'action',
      render: (_, row) => (
        <Button type="link" onClick={() => setViewingPayslip(row)}>
          {MESSAGES.common.view}
        </Button>
      ),
    },
  ];

  const actions = d ? (
    <Space>
      {d.hasImportedFile ? (
        <Button
          icon={<DownloadOutlined />}
          loading={downloadFile.isPending}
          onClick={() =>
            downloadFile.mutate({ payrollId: d.payrollId, periodMonth: d.periodMonth })
          }
        >
          {PAYROLL_TEXT.downloadImportedFile}
        </Button>
      ) : null}
      {canReopen ? (
        <Button onClick={() => setReopening(true)}>{PAYROLL_TEXT.actions.reopen}</Button>
      ) : null}
      {canReject ? (
        <Button danger onClick={() => setRejecting(true)}>
          {PAYROLL_TEXT.actions.reject}
        </Button>
      ) : null}
      {canApprove ? (
        <Popconfirm
          title={MESSAGES.payroll.confirmApprove}
          onConfirm={() => approve.mutate(undefined, { onSuccess: onClose })}
        >
          <Button type="primary" loading={approve.isPending}>
            {PAYROLL_TEXT.actions.approve}
          </Button>
        </Popconfirm>
      ) : null}
    </Space>
  ) : null;

  return (
    <DetailModal
      open={payrollId !== null}
      onClose={onClose}
      title={d ? `${PAYROLL_TEXT.periodMonth} ${formatMonth(d.periodMonth)}` : undefined}
      extra={actions}
    >
      {detail.isLoading ? <Spin /> : null}
      {detail.error ? <Alert type="error" showIcon message={detail.error.message} /> : null}

      {d ? (
        <>
          <Descriptions
            column={{ xs: 1, sm: 2, md: 3 }}
            bordered
            size="small"
            style={{ marginBottom: 16 }}
          >
            <Descriptions.Item label={PAYROLL_TEXT.summary.status}>
              <Tag color={PAYROLL_STATUS_LABEL[d.status].color}>
                {PAYROLL_STATUS_LABEL[d.status].text}
              </Tag>
            </Descriptions.Item>
            <Descriptions.Item label={PAYROLL_TEXT.summary.totalAmount}>
              {formatCurrency(d.totalAmount)}
            </Descriptions.Item>
            <Descriptions.Item label={PAYROLL_TEXT.summary.payslipCount}>
              {d.payslips.length}
            </Descriptions.Item>
            {d.submittedAt ? (
              <Descriptions.Item label={PAYROLL_TEXT.summary.submittedAt}>
                {formatDateTime(d.submittedAt)}
              </Descriptions.Item>
            ) : null}
            {d.approvedAt ? (
              <Descriptions.Item label={PAYROLL_TEXT.summary.approvedAt}>
                {formatDateTime(d.approvedAt)}
              </Descriptions.Item>
            ) : null}
          </Descriptions>

          {d.rejectReason ? (
            <Alert
              type="warning"
              showIcon
              style={{ marginBottom: 16 }}
              message={`${PAYROLL_TEXT.summary.rejectReason}: ${d.rejectReason}`}
            />
          ) : null}
          {d.reopenReason ? (
            <Alert
              type="info"
              showIcon
              style={{ marginBottom: 16 }}
              message={`${PAYROLL_TEXT.summary.reopenReason}: ${d.reopenReason}`}
            />
          ) : null}

          <DataTable<Payslip>
            rowKey="payslipId"
            size="small"
            columns={columns}
            dataSource={d.payslips}
            pagination={{ pageSize: 10 }}
            scroll={{ x: 'max-content' }}
          />
        </>
      ) : null}

      <ReasonModal
        open={rejecting}
        title={PAYROLL_TEXT.actions.reject}
        loading={reject.isPending}
        onCancel={() => setRejecting(false)}
        onSubmit={(reason) =>
          reject.mutate(
            { reason },
            {
              onSuccess: () => {
                setRejecting(false);
                onClose();
              },
            },
          )
        }
      />
      <ReasonModal
        open={reopening}
        title={PAYROLL_TEXT.actions.reopen}
        loading={reopen.isPending}
        onCancel={() => setReopening(false)}
        onSubmit={(reason) =>
          reopen.mutate(
            { reason },
            {
              onSuccess: () => {
                setReopening(false);
                onClose();
              },
            },
          )
        }
      />

      <Modal
        open={viewingPayslip !== null}
        title={
          viewingPayslip ? `${viewingPayslip.employeeCode} - ${viewingPayslip.fullName}` : undefined
        }
        onCancel={() => setViewingPayslip(null)}
        footer={null}
        width={MODAL_WIDTH.md}
        destroyOnHidden
      >
        {viewingPayslip ? (
          <PayslipItemsView
            workDays={viewingPayslip.workDays}
            totalAdditions={viewingPayslip.totalAdditions}
            totalDeductions={viewingPayslip.totalDeductions}
            insurance={viewingPayslip.insurance}
            netPay={viewingPayslip.netPay}
            items={viewingPayslip.items}
          />
        ) : null}
      </Modal>
    </DetailModal>
  );
}
