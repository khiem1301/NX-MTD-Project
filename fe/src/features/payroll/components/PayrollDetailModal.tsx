import { useState } from 'react';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { Alert, Button, Modal, Space, Spin } from 'antd';
import { ConfirmAction } from '@/components/common/ConfirmAction';
import { DownloadOutlined } from '@ant-design/icons';
import { ReasonModal } from '@/components/common/ReasonModal';
import { MESSAGES } from '@/constants/messages';
import { formatMonth } from '@/utils/format';
import { PAYROLL_TEXT } from '../constants';
import {
  useApprovePayroll,
  useDownloadImportedFile,
  usePayrollDetail,
  useRejectPayroll,
  useReopenPayroll,
} from '../hooks/usePayroll';
import type { Payslip } from '../types';
import { PayslipItemsView } from './PayslipItemsView';
import { DetailModal } from '@/components/common/DetailModal';
import { DataTable } from '@/components/common/DataTable';
import { payslipColumns } from './payslipColumns';
import { PayrollDetailInfo } from './PayrollDetailInfo';

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

  const columns = payslipColumns({ onView: setViewingPayslip });

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
        <ConfirmAction
          title={MESSAGES.payroll.confirmApprove}
          onConfirm={() => approve.mutate(undefined, { onSuccess: onClose })}
        >
          <Button type="primary" loading={approve.isPending}>
            {PAYROLL_TEXT.actions.approve}
          </Button>
        </ConfirmAction>
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
          <PayrollDetailInfo detail={d} />

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
