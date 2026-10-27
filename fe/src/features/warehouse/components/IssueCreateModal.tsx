import { DatePicker, Form, Modal, Radio, Select, Space } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { OrderSelect } from '@/components/common/OrderSelect';
import { MESSAGES } from '@/constants/messages';
import { useTeamOptions } from '@/hooks/useLookups';
import { formatDate } from '@/utils/format';
import { LABELS, TEAM_TYPE, WAREHOUSE_TYPE } from '../constants';
import { useCreateIssue, useIssuableWorkOrders } from '../hooks/useIssues';
import type { CreateIssuePayload } from '../types';
import { FabricIssueLines } from './FabricIssueLines';
import { parsePieceKey } from '../utils';
import { PieceIssueLines } from './PieceIssueLines';
import { RequisitionSelect } from './RequisitionSelect';

type IssueCreateModalProps = { open: boolean; onClose: () => void };
type FormValues = Omit<CreateIssuePayload, 'issueDate' | 'lines'> & {
  issueDate: Dayjs;
  lines: (CreateIssuePayload['lines'][number] & { pieceKey?: string })[];
};

const DATE_FORMAT = 'DD/MM/YYYY';
const API_DATE = 'YYYY-MM-DD';

/** UC40: Thủ kho cấp vải cho tổ cắt (theo lệnh cắt) hoặc BTP cho tổ may; số cấp được giữ chỗ trong kho. */
export function IssueCreateModal({ open, onClose }: IssueCreateModalProps) {
  const [form] = Form.useForm<FormValues>();
  const create = useCreateIssue();
  const type = Form.useWatch('warehouseType', form);
  const workOrderId = Form.useWatch('workOrderId', form);
  const orderId = Form.useWatch('orderId', form);
  const isFabric = type === WAREHOUSE_TYPE.MATERIAL;
  const workOrders = useIssuableWorkOrders(open && isFabric);
  const sewingTeams = useTeamOptions(TEAM_TYPE.SEWING);


  const handleFinish = (values: FormValues) => {
    const body: CreateIssuePayload = {
      ...values,
      issueDate: values.issueDate.format(API_DATE),
      // Dòng BTP chọn theo "dòng đơn:chi tiết" → tách ra orderLineId + productComponentMaterialId.
      lines: (values.lines ?? []).map(({ pieceKey, ...line }) => (pieceKey ? { ...line, ...parsePieceKey(pieceKey) } : line)),
    };
    create.mutate(body, {
      onSuccess: () => {
        form.resetFields();
        onClose();
      },
    });
  };
  const required = [{ required: true, message: MESSAGES.common.required }];

  return (
    <Modal
      open={open}
      width={920}
      title={isFabric ? LABELS.issueFabric : LABELS.issuePieces}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={create.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Form
        form={form}
        layout="vertical"
        onFinish={handleFinish}
        disabled={create.isPending}
        // Form dựng lại mỗi lần mở (destroyOnHidden) nên giá trị mặc định đặt ở đây, không dùng setFieldsValue trước khi dựng.
        initialValues={{ warehouseType: WAREHOUSE_TYPE.MATERIAL, issueDate: dayjs() }}
      >
        <Form.Item name="warehouseType">
          <Radio.Group
            optionType="button"
            onChange={() =>
              form.setFieldsValue({ lines: [], workOrderId: undefined, orderId: undefined })
            }
            options={[
              { value: WAREHOUSE_TYPE.MATERIAL, label: LABELS.issueFabric },
              { value: WAREHOUSE_TYPE.SEMI_FINISHED, label: LABELS.issuePieces },
            ]}
          />
        </Form.Item>
        <Space wrap align="start">
          {isFabric ? (
            <Form.Item name="workOrderId" label={LABELS.workOrder} rules={required}>
              <Select
                showSearch
                optionFilterProp="label"
                loading={workOrders.isLoading}
                style={{ width: 380 }}
                options={(workOrders.data ?? []).map((w) => ({
                  value: w.workOrderId,
                  label: `${w.workOrderCode} · ${w.orderCode} · ${w.productCode} · ${w.cuttingTeamName} (${formatDate(w.dueDate)})`,
                }))}
              />
            </Form.Item>
          ) : (
            <>
              <Form.Item name="orderId" label={LABELS.order} rules={required}>
                <OrderSelect onChange={() => form.setFieldValue('lines', [])} />
              </Form.Item>
              <Form.Item name="receivingTeamId" label={LABELS.receivingTeam} rules={required}>
                <Select
                  style={{ width: 200 }}
                  options={(sewingTeams.data ?? []).map((t) => ({
                    value: t.teamId,
                    label: t.teamName,
                  }))}
                />
              </Form.Item>
            </>
          )}
          <Form.Item name="issueDate" label={LABELS.issueDate} rules={required}>
            <DatePicker format={DATE_FORMAT} />
          </Form.Item>
          <Form.Item
            name="requestId"
            label={isFabric ? LABELS.requestIdOptional : LABELS.requestIdRequired}
            rules={isFabric ? [] : required}
          >
            <RequisitionSelect requestType={isFabric ? 'MaterialRequisition' : 'SemiFinishedRequisition'} />
          </Form.Item>
        </Space>
        {isFabric ? (
          <FabricIssueLines form={form} workOrderId={workOrderId} />
        ) : (
          <PieceIssueLines orderId={orderId} />
        )}
      </Form>
    </Modal>
  );
}
