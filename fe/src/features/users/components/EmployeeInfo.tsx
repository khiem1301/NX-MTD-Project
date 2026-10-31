import { Descriptions } from 'antd';
import { DetailDescriptions } from '@/components/common/DetailModal';
import { StatusTag } from '@/components/common/StatusTag';
import { EMPLOYEE_STATUS_TAG, PAY_TYPE_LABEL, ROLE_LABEL } from '@/constants/hr';
import { MESSAGES } from '@/constants/messages';
import { formatDate } from '@/utils/format';
import type { EmployeeDetail } from '../types';

/** Thông tin chung của nhân viên: vai trò, tổ, trạng thái, hình thức trả lương, bậc, liên hệ, mốc ngày. */
export function EmployeeInfo({ employee: data }: { employee: EmployeeDetail }) {
  return (
    <DetailDescriptions style={{ marginBottom: 16 }}>
      <Descriptions.Item label={MESSAGES.hr.role}>
        {ROLE_LABEL[data.role] ?? data.role}
      </Descriptions.Item>
      <Descriptions.Item label={MESSAGES.hr.team}>{data.teamName ?? '—'}</Descriptions.Item>
      <Descriptions.Item label={MESSAGES.hr.status}>
        <StatusTag status={data.status} labels={EMPLOYEE_STATUS_TAG} />
      </Descriptions.Item>
      <Descriptions.Item label={MESSAGES.hr.payType}>
        {PAY_TYPE_LABEL[data.payType] ?? data.payType}
      </Descriptions.Item>
      <Descriptions.Item label={MESSAGES.hr.grade}>
        {data.currentGradeCode ?? '—'}
      </Descriptions.Item>
      <Descriptions.Item label={MESSAGES.hr.attendanceCode}>
        {data.attendanceCode ?? '—'}
      </Descriptions.Item>
      <Descriptions.Item label={MESSAGES.hr.phone}>{data.phone ?? '—'}</Descriptions.Item>
      <Descriptions.Item label={MESSAGES.hr.dateOfBirth}>
        {formatDate(data.dateOfBirth)}
      </Descriptions.Item>
      <Descriptions.Item label={MESSAGES.hr.startDate}>
        {formatDate(data.startDate)}
      </Descriptions.Item>
      {data.resignDate && (
        <Descriptions.Item label={MESSAGES.hr.resignDate}>
          {formatDate(data.resignDate)}
        </Descriptions.Item>
      )}
    </DetailDescriptions>
  );
}
