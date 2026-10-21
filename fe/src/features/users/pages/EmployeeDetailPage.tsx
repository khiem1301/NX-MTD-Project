import { Alert, Button, Card, Descriptions, Modal, Popconfirm, Result, Space, Spin, Tabs, DatePicker, Form, Input } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { NoteModal } from '@/components/common/NoteModal';
import { QueryError } from '@/components/common/QueryError';
import { StatusTag } from '@/components/common/StatusTag';
import { AuditHistory } from '@/components/common/AuditHistory';
import { ACCOUNT_STATUS_TAG, EMPLOYEE_STATUS_TAG, PAY_TYPE_LABEL, ROLE_LABEL } from '@/constants/hr';
import { ERROR_CODES } from '@/constants/errorCodes';
import { MESSAGES } from '@/constants/messages';
import { ROLES } from '@/constants/roles';
import { ROUTES } from '@/constants/routes';
import { useAuth } from '@/hooks/useAuth';
import type { ApiError } from '@/types/api';
import { formatDate, formatDateTime, toApiDate } from '@/utils/format';
import { EmployeeFormDrawer } from '../components/EmployeeFormDrawer';
import { TempPasswordModal } from '../components/TempPasswordModal';
import { useEmployee, useLockAccount, useResetPassword, useResignEmployee, useUnlockAccount } from '../hooks/useEmployees';
import type { TempPassword } from '../types';

type ResignForm = { resignDate: Dayjs; reason: string };

/** Chi tiết nhân viên + lịch sử. Admin có thêm khối tài khoản (khóa / mở khóa / đặt lại mật khẩu) và Cho nghỉ việc. */
export function EmployeeDetailPage() {
  const { id } = useParams();
  const employeeId = Number(id);
  const { user } = useAuth();
  const isAdmin = user?.role === ROLES.Admin;
  const navigate = useNavigate();
  const { data, isLoading, error, refetch } = useEmployee(Number.isInteger(employeeId) ? employeeId : null);
  const lock = useLockAccount(employeeId);
  const unlock = useUnlockAccount(employeeId);
  const reset = useResetPassword(employeeId);
  const resign = useResignEmployee(employeeId);
  const [editing, setEditing] = useState(false);
  const [locking, setLocking] = useState(false);
  const [resigning, setResigning] = useState(false);
  const [tempPassword, setTempPassword] = useState<TempPassword | null>(null);
  const [resignForm] = Form.useForm<ResignForm>();

  if (isLoading) return <Spin />;
  if ((error as ApiError | null)?.errorCode === ERROR_CODES.CommonNotFound) return <Result status="404" subTitle={MESSAGES.common.notFound} />;
  if (error) return <QueryError error={error} onRetry={() => void refetch()} />;
  if (!data) return null;

  const isSelf = data.employeeId === user?.employeeId;
  const active = data.status === 'Active';
  const account = data.account;

  return (
    <Card
      title={`${data.employeeCode} · ${data.fullName}`}
      extra={
        <Space wrap>
          <Button onClick={() => navigate(isAdmin ? ROUTES.adminEmployees : ROUTES.managerEmployees)}>{MESSAGES.common.close}</Button>
          {active && <Button onClick={() => setEditing(true)}>{MESSAGES.common.edit}</Button>}
          {isAdmin && active && !isSelf && <Button danger onClick={() => setResigning(true)}>{MESSAGES.hr.resign}</Button>}
        </Space>
      }
    >
      <Descriptions size="small" bordered column={3} style={{ marginBottom: 16 }}>
        <Descriptions.Item label={MESSAGES.hr.role}>{ROLE_LABEL[data.role] ?? data.role}</Descriptions.Item>
        <Descriptions.Item label={MESSAGES.hr.team}>{data.teamName ?? '—'}</Descriptions.Item>
        <Descriptions.Item label={MESSAGES.hr.status}><StatusTag status={data.status} labels={EMPLOYEE_STATUS_TAG} /></Descriptions.Item>
        <Descriptions.Item label={MESSAGES.hr.payType}>{PAY_TYPE_LABEL[data.payType] ?? data.payType}</Descriptions.Item>
        <Descriptions.Item label={MESSAGES.hr.grade}>{data.currentGradeCode ?? '—'}</Descriptions.Item>
        <Descriptions.Item label={MESSAGES.hr.attendanceCode}>{data.attendanceCode ?? '—'}</Descriptions.Item>
        <Descriptions.Item label={MESSAGES.hr.phone}>{data.phone ?? '—'}</Descriptions.Item>
        <Descriptions.Item label={MESSAGES.hr.dateOfBirth}>{formatDate(data.dateOfBirth)}</Descriptions.Item>
        <Descriptions.Item label={MESSAGES.hr.startDate}>{formatDate(data.startDate)}</Descriptions.Item>
        {data.resignDate && <Descriptions.Item label={MESSAGES.hr.resignDate}>{formatDate(data.resignDate)}</Descriptions.Item>}
      </Descriptions>

      {isAdmin && account && (
        <Card size="small" title={MESSAGES.hr.account} style={{ marginBottom: 16 }}>
          <Space wrap>
            <span>{`${MESSAGES.hr.username}: ${account.username}`}</span>
            <StatusTag status={account.status} labels={ACCOUNT_STATUS_TAG} />
            {account.isTemporaryPassword && <Alert type="info" message={MESSAGES.hr.tempPasswordNotice} banner />}
            {account.lockedAt && <span>{`${formatDateTime(account.lockedAt)} · ${account.lockReason ?? ''}`}</span>}
            {!isSelf && account.status === 'Active' && <Button danger onClick={() => setLocking(true)}>{MESSAGES.hr.lockAccount}</Button>}
            {!isSelf && account.status === 'Locked' && active && <Button onClick={() => unlock.mutate(undefined)}>{MESSAGES.hr.unlockAccount}</Button>}
            {!isSelf && active && (
              <Popconfirm title={MESSAGES.hr.resetConfirm} onConfirm={() => reset.mutate(undefined, { onSuccess: setTempPassword })}>
                <Button>{MESSAGES.hr.resetPassword}</Button>
              </Popconfirm>
            )}
          </Space>
        </Card>
      )}

      <Tabs items={[{ key: 'history', label: MESSAGES.hr.history, children: <AuditHistory history={data.history} /> }]} />

      <EmployeeFormDrawer open={editing} employee={data} isAdmin={isAdmin} onClose={() => setEditing(false)} onCreated={setTempPassword} />
      <TempPasswordModal value={tempPassword} onClose={() => setTempPassword(null)} />
      {locking && (
        <NoteModal open title={MESSAGES.hr.lockAccount} label={MESSAGES.hr.lockReason} required danger loading={lock.isPending}
          onCancel={() => setLocking(false)}
          onSubmit={(note) => lock.mutate(note ?? '', { onSettled: () => setLocking(false) })} />
      )}
      <Modal
        open={resigning}
        title={MESSAGES.hr.resign}
        okText={MESSAGES.common.confirm}
        cancelText={MESSAGES.common.cancel}
        okButtonProps={{ danger: true }}
        confirmLoading={resign.isPending}
        onCancel={() => setResigning(false)}
        onOk={() => resignForm.submit()}
        destroyOnHidden
      >
        <Form form={resignForm} layout="vertical" preserve={false} initialValues={{ resignDate: dayjs() }}
          onFinish={(v) => resign.mutate({ resignDate: toApiDate(v.resignDate), reason: v.reason.trim(), rowVer: data.rowVer }, { onSuccess: () => setResigning(false) })}>
          <Form.Item name="resignDate" label={MESSAGES.hr.resignDate} rules={[{ required: true, message: MESSAGES.common.required }]}>
            <DatePicker format="DD/MM/YYYY" />
          </Form.Item>
          <Form.Item name="reason" label={MESSAGES.hr.resignReason} rules={[{ required: true, whitespace: true, message: MESSAGES.common.reasonRequired }]}>
            <Input.TextArea rows={3} maxLength={500} showCount />
          </Form.Item>
        </Form>
      </Modal>
    </Card>
  );
}
