import { Form, Input, Modal, Typography, message, type FormInstance } from 'antd';
import { useQueryClient } from '@tanstack/react-query';
import { DataTable } from '@/components/common/DataTable';
import { WorkOrderSelect } from '@/components/common/WorkOrderSelect';
import { MESSAGES } from '@/constants/messages';
import { REQUESTS_KEY } from '@/hooks/useRequests';
import type { WorkOrderMaterialAllowance } from '@/types/workOrder';
import { formatQty } from '@/utils/format';
import { workOrderRequisitionApi } from '../api/workOrderRequisitionApi';
import { useTeamWorkOrders } from '../hooks/useWorkOrderRequisition';
import { EMPTY_LINE } from '../hooks/useRequestForm';
import { suggestLines } from '../utils/workOrderRequisition';

type WorkOrderRequisitionFieldProps = {
  form: FormInstance;
  allowance: WorkOrderMaterialAllowance[] | undefined;
  /** Sửa đơn: lệnh cố định, không đổi được. */
  locked: boolean;
};

const qty = (v: number | null) =>
  v === null ? MESSAGES.requests.allowanceNotComputed : formatQty(v);

/**
 * Đơn xin NVL của tổ cắt (DEV-ALL-92): chọn lệnh cắt (không bắt buộc) để lấy vải và số mét hệ thống tính cho lệnh;
 * bảng dưới cho biết trần, các đơn khác đã xin và còn xin được của từng vải.
 */
export function WorkOrderRequisitionField({
  form,
  allowance,
  locked,
}: WorkOrderRequisitionFieldProps) {
  const queryClient = useQueryClient();
  const workOrders = useTeamWorkOrders(true);

  const fill = (lines: { materialId: number; quantity: number }[]) => {
    if (lines.length === 0) void message.warning(MESSAGES.requests.noRemaining);
    form.setFieldValue('lines', lines.length > 0 ? lines : [EMPTY_LINE]);
  };

  const handleChange = async (workOrderId: number | undefined) => {
    const wo = workOrders.data?.find((w) => w.workOrderId === workOrderId);
    form.setFieldsValue({ workOrderId, orderId: wo?.orderId });
    if (workOrderId === undefined) return;
    const rows = await queryClient.fetchQuery({
      queryKey: [...REQUESTS_KEY, 'work-order-allowance', workOrderId, undefined],
      queryFn: () => workOrderRequisitionApi.allowance(workOrderId),
    });
    const lines = suggestLines(rows);
    const typed = ((form.getFieldValue('lines') ?? []) as { materialId?: number }[]).some(
      (l) => l.materialId,
    );
    if (!typed) return fill(lines);
    Modal.confirm({
      title: MESSAGES.requests.replaceLinesTitle,
      content: MESSAGES.requests.replaceLinesContent,
      okText: MESSAGES.common.confirm,
      cancelText: MESSAGES.common.cancel,
      onOk: () => fill(lines),
    });
  };

  return (
    <>
      <Form.Item label={MESSAGES.requests.workOrder}>
        <Form.Item name="workOrderId" noStyle>
          <WorkOrderSelect
            width="100%"
            workOrders={workOrders.data}
            loading={workOrders.isLoading}
            disabled={locked}
            placeholder={MESSAGES.requests.workOrderPlaceholder}
            onChange={(id) => void handleChange(id)}
          />
        </Form.Item>
        <Form.Item name="orderId" noStyle>
          <Input type="hidden" />
        </Form.Item>
      </Form.Item>
      {allowance && allowance.length > 0 && (
        <DataTable<WorkOrderMaterialAllowance>
          rowKey="materialId"
          size="small"
          pagination={false}
          style={{ marginBottom: 16 }}
          dataSource={allowance}
          columns={[
            {
              title: MESSAGES.requests.allowanceMaterial,
              key: 'material',
              render: (_, a) => (
                <Typography.Text>
                  {`${a.materialCode} ${a.materialName}${a.materialColor ? ` - ${a.materialColor}` : ''}`}
                </Typography.Text>
              ),
            },
            {
              title: MESSAGES.requests.allowanceTotal,
              align: 'right',
              render: (_, a) => qty(a.capQty),
            },
            {
              title: MESSAGES.requests.allowanceRemaining,
              align: 'right',
              render: (_, a) => <strong>{qty(a.remainingQty)}</strong>,
            },
          ]}
        />
      )}
    </>
  );
}
