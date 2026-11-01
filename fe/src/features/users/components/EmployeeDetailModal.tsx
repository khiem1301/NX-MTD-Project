import { useState } from 'react';
import { Alert, Button, Card, Result, Space, Tabs } from 'antd';
import { ConfirmAction } from '@/components/common/ConfirmAction';
import { AuditHistory } from '@/components/common/AuditHistory';
import { DetailModal } from '@/components/common/DetailModal';
import { NoteModal } from '@/components/common/NoteModal';
import { StatusTag } from '@/components/common/StatusTag';
import { ERROR_CODES } from '@/constants/errorCodes';
import { ACCOUNT_STATUS_TAG } from '@/constants/hr';
import { MESSAGES } from '@/constants/messages';
import { ROLES } from '@/constants/roles';
import { useAuth } from '@/hooks/useAuth';
import { useQueryId } from '@/hooks/useQueryId';
import type { ApiError } from '@/types/api';
import { formatDateTime } from '@/utils/format';
import {
  useEmployee,
  useLockAccount,
  useResetPassword,
  useUnlockAccount,
} from '../hooks/useEmployees';
import type { TempPassword } from '../types';
import { EmployeeFormModal } from './EmployeeFormModal';
import { TempPasswordModal } from './TempPasswordModal';
import { EmployeeInfo } from './EmployeeInfo';
import { ResignEmployeeModal } from './ResignEmployeeModal';

/** Chi tiết nhân viên mở theo ?id= + lịch sử. Admin có thêm khối tài khoản (khóa / mở khóa / đặt lại mật khẩu) và Cho nghỉ việc. */
export function EmployeeDetailModal() {
  const [id, close] = useQueryId();
  const { user } = useAuth();
  const isAdmin = user?.role === ROLES.Admin;
  // Quản đốc quản lý tài khoản của mọi người trừ Admin và Kế toán (BE chặn phần còn lại).
  const canManage = isAdmin || user?.role === ROLES.Manager;
  const { data, isLoading, error } = useEmployee(id);
  const lock = useLockAccount(id ?? 0);
  const unlock = useUnlockAccount(id ?? 0);
  const reset = useResetPassword(id ?? 0);
  const [editing, setEditing] = useState(false);
  const [locking, setLocking] = useState(false);
  const [resigning, setResigning] = useState(false);
  const [tempPassword, setTempPassword] = useState<TempPassword | null>(null);
  const notFound = (error as ApiError | null)?.errorCode === ERROR_CODES.CommonNotFound;

  const isSelf = data?.employeeId === user?.employeeId;
  const active = data?.status === 'Active';
  const account = data?.account;

  const actions = data && (
    <>
      {active && <Button onClick={() => setEditing(true)}>{MESSAGES.common.edit}</Button>}
      {canManage && active && !isSelf && (
        <Button danger onClick={() => setResigning(true)}>
          {MESSAGES.hr.resign}
        </Button>
      )}
    </>
  );

  return (
    <DetailModal
      open={id !== null}
      onClose={close}
      title={data ? `${data.employeeCode} · ${data.fullName}` : undefined}
      extra={actions}
      loading={isLoading}
      error={notFound ? null : error}
    >
      {notFound && <Result status="404" subTitle={MESSAGES.common.notFound} />}
      {data && (
        <>
          <EmployeeInfo employee={data} />

          {canManage && account && (
            <Card size="small" title={MESSAGES.hr.account} style={{ marginBottom: 16 }}>
              <Space wrap>
                <span>{`${MESSAGES.hr.username}: ${account.username}`}</span>
                <StatusTag status={account.status} labels={ACCOUNT_STATUS_TAG} />
                {account.isTemporaryPassword && (
                  <Alert type="info" message={MESSAGES.hr.tempPasswordNotice} banner />
                )}
                {account.lockedAt && (
                  <span>{`${formatDateTime(account.lockedAt)} · ${account.lockReason ?? ''}`}</span>
                )}
                {!isSelf && account.status === 'Active' && (
                  <Button danger onClick={() => setLocking(true)}>
                    {MESSAGES.hr.lockAccount}
                  </Button>
                )}
                {!isSelf && account.status === 'Locked' && active && (
                  <Button onClick={() => unlock.mutate(undefined)}>
                    {MESSAGES.hr.unlockAccount}
                  </Button>
                )}
                {!isSelf && active && (
                  <ConfirmAction
                    title={MESSAGES.hr.resetConfirm}
                    onConfirm={() => reset.mutate(undefined, { onSuccess: setTempPassword })}
                  >
                    <Button>{MESSAGES.hr.resetPassword}</Button>
                  </ConfirmAction>
                )}
              </Space>
            </Card>
          )}

          <Tabs
            items={[
              {
                key: 'history',
                label: MESSAGES.hr.history,
                children: <AuditHistory history={data.history} />,
              },
            ]}
          />

          <EmployeeFormModal
            open={editing}
            employee={data}
            isAdmin={isAdmin}
            onClose={() => setEditing(false)}
            onCreated={setTempPassword}
          />
          {locking && (
            <NoteModal
              open
              title={MESSAGES.hr.lockAccount}
              label={MESSAGES.hr.lockReason}
              required
              danger
              loading={lock.isPending}
              onCancel={() => setLocking(false)}
              onSubmit={(note) => lock.mutate(note ?? '', { onSettled: () => setLocking(false) })}
            />
          )}
          <ResignEmployeeModal
            employeeId={data.employeeId}
            rowVer={data.rowVer}
            open={resigning}
            onClose={() => setResigning(false)}
          />
        </>
      )}
      <TempPasswordModal value={tempPassword} onClose={() => setTempPassword(null)} />
    </DetailModal>
  );
}
