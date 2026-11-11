import { Flex, Form, Select } from 'antd';
import { AppDatePicker } from '@/components/common/DateInputs';
import { OrderSelect } from '@/components/common/OrderSelect';
import { MESSAGES } from '@/constants/messages';
import type { RequestDetail } from '@/types/request';
import type { TeamOption } from '@/types/lookup';
import type { WorkOrderListItem } from '@/types/workOrder';
import { formatDate } from '@/utils/format';
import { ISSUE_FIELD, LABELS } from '../constants';
import { RequisitionSelect } from './RequisitionSelect';

type IssueTargetFieldsProps = {
  isMaterial: boolean;
  request: RequestDetail | null;
  workOrders: WorkOrderListItem[];
  workOrdersLoading: boolean;
  teams: TeamOption[];
  teamLocked: boolean;
  orderLocked: boolean;
  onRequestChange: (requestId: number | undefined) => void;
  onWorkOrderChange: (workOrderId: number | undefined) => void;
  onOrderChange: () => void;
};

/** Độ rộng ô vừa nội dung (không giãn hết khung): mã đơn xin / lệnh cắt dài hơn tên tổ, mã đơn hàng. */
const WIDTH = { request: 280, workOrder: 300, team: 170, order: 220 } as const;
const required = [{ required: true, message: MESSAGES.common.required }];

/**
 * Thông tin phiếu cấp: nhãn cùng dòng với ô, ô rộng vừa nội dung, xếp nối nhau và tự xuống dòng khi hết chỗ. Ô đã lấy từ
 * đơn xin / lệnh cắt bị khóa để không chọn lệch.
 */
export function IssueTargetFields({
  isMaterial,
  request,
  workOrders,
  workOrdersLoading,
  teams,
  teamLocked,
  orderLocked,
  onRequestChange,
  onWorkOrderChange,
  onOrderChange,
}: IssueTargetFieldsProps) {
  return (
    <Flex wrap style={{ columnGap: 24 }}>
      <Form.Item
        name="requestId"
        style={ISSUE_FIELD}
        label={isMaterial ? LABELS.requestIdOptional : LABELS.requestIdPieces}
      >
        <RequisitionSelect
          width={WIDTH.request}
          requestType={isMaterial ? 'MaterialRequisition' : 'SemiFinishedRequisition'}
          placeholder={LABELS.requestPlaceholder}
          onChange={onRequestChange}
        />
      </Form.Item>
      {isMaterial && (
        <Form.Item name="workOrderId" style={ISSUE_FIELD} label={LABELS.workOrderOptional}>
          <Select
            showSearch
            allowClear
            optionFilterProp="label"
            loading={workOrdersLoading}
            disabled={(request?.workOrderId ?? null) !== null}
            placeholder={LABELS.workOrderPlaceholder}
            style={{ width: WIDTH.workOrder }}
            popupMatchSelectWidth={false}
            onChange={onWorkOrderChange}
            options={workOrders.map((w) => ({
              value: w.workOrderId,
              label: `${w.workOrderCode} · ${w.orderCode} · ${w.productCode} · ${w.cuttingTeamName} (${formatDate(w.dueDate)})`,
            }))}
          />
        </Form.Item>
      )}
      <Form.Item
        name="receivingTeamId"
        style={ISSUE_FIELD}
        label={LABELS.receivingTeam}
        rules={required}
      >
        <Select
          disabled={teamLocked}
          style={{ width: WIDTH.team }}
          options={teams.map((t) => ({ value: t.teamId, label: t.teamName }))}
        />
      </Form.Item>
      <Form.Item name="orderId" style={ISSUE_FIELD} label={LABELS.order} rules={required}>
        <OrderSelect width={WIDTH.order} disabled={orderLocked} onChange={onOrderChange} />
      </Form.Item>
      <Form.Item name="issueDate" style={ISSUE_FIELD} label={LABELS.issueDate} rules={required}>
        <AppDatePicker />
      </Form.Item>
    </Flex>
  );
}
