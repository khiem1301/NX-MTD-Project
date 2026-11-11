import { Form, Modal, Radio, message } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { MESSAGES } from '@/constants/messages';
import { useTeamOptions } from '@/hooks/useLookups';
import { API_DATE } from '@/utils/format';
import { ISSUE_FIELD, LABELS, TEAM_TYPE, WAREHOUSE_TYPE } from '../constants';
import { useCreateIssue, useIssuableWorkOrders } from '../hooks/useIssues';
import { useIssueRequestFill } from '../hooks/useIssueRequestFill';
import { requestedQtyMap } from '../utils/requestedQty';
import type { CreateIssuePayload } from '../types';
import { MaterialIssueLines } from './MaterialIssueLines';
import { parsePieceKey } from '../utils/pieceKey';
import { PieceIssueLines } from './PieceIssueLines';
import { IssueTargetFields } from './IssueTargetFields';
import { MODAL_WIDTH } from '@/components/common/modalSize';

type IssueCreateModalProps = { open: boolean; onClose: () => void };
type FormValues = Omit<CreateIssuePayload, 'issueDate' | 'lines'> & {
  issueDate: Dayjs;
  lines: (CreateIssuePayload['lines'][number] & { pieceKey?: string })[];
};

const RESET_TARGET = {
  lines: [],
  workOrderId: undefined,
  orderId: undefined,
  receivingTeamId: undefined,
  requestId: undefined,
};

/**
 * UC40: Thủ kho cấp NVL (vải, chỉ, cúc…) cho tổ cắt / tổ may, hoặc BTP cho tổ may; số cấp được giữ chỗ trong kho.
 * Lệnh cắt và đơn xin đều không bắt buộc (DEV-ALL-99). Chọn đơn xin thì tổ, đơn hàng, lệnh cắt và số lượng được điền sẵn.
 * Lệnh cắt đã hoàn thành không có trong danh sách chọn.
 */
export function IssueCreateModal({ open, onClose }: IssueCreateModalProps) {
  const [form] = Form.useForm<FormValues>();
  const create = useCreateIssue();
  const type = Form.useWatch('warehouseType', form);
  const workOrderId = Form.useWatch('workOrderId', form);
  const orderId = Form.useWatch('orderId', form);
  const teamId = Form.useWatch('receivingTeamId', form);
  const isMaterial = type === WAREHOUSE_TYPE.MATERIAL;
  const workOrders = useIssuableWorkOrders(open && isMaterial);
  const cuttingTeams = useTeamOptions(TEAM_TYPE.CUTTING);
  const sewingTeams = useTeamOptions(TEAM_TYPE.SEWING);
  const fromRequest = useIssueRequestFill(form, isMaterial);
  const { request } = fromRequest;

  const teams = [...(isMaterial ? (cuttingTeams.data ?? []) : []), ...(sewingTeams.data ?? [])];
  const teamLocked = workOrderId !== undefined || (request?.createdByTeamId ?? null) !== null;
  const orderLocked = workOrderId !== undefined || (request?.orderId ?? null) !== null;
  const selectable = (workOrders.data ?? []).filter(
    (w) => teamId === undefined || w.cuttingTeamId === teamId,
  );

  const handleWorkOrderChange = (id: number | undefined) => {
    const wo = workOrders.data?.find((w) => w.workOrderId === id);
    if (wo) form.setFieldsValue({ orderId: wo.orderId, receivingTeamId: wo.cuttingTeamId });
  };

  const handleFinish = (values: FormValues) => {
    // Chỉ gửi các dòng Thủ kho đã nhập số cấp.
    const lines = (values.lines ?? []).filter((l) => (l.issuedQty ?? 0) > 0);
    if (lines.length === 0) {
      void message.warning(isMaterial ? LABELS.issueNeedMaterialLine : LABELS.issueNeedLine);
      return;
    }
    const body: CreateIssuePayload = {
      ...values,
      issueDate: values.issueDate.format(API_DATE),
      // Dòng BTP chọn theo "dòng đơn:chi tiết" → tách ra orderLineId + productComponentMaterialId.
      lines: lines.map(({ pieceKey, ...line }) =>
        pieceKey ? { ...line, ...parsePieceKey(pieceKey) } : line,
      ),
    };
    create.mutate(body, {
      onSuccess: () => {
        form.resetFields();
        fromRequest.clear();
        onClose();
      },
    });
  };

  return (
    <Modal
      open={open}
      width={MODAL_WIDTH.lg}
      title={isMaterial ? LABELS.issueMaterialTitle : LABELS.issuePiecesTitle}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={create.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Form
        form={form}
        // Nhãn cùng dòng với ô nhập, rộng vừa chữ; ô nhập lấy hết phần còn lại của cột.
        layout="horizontal"
        size="small"
        colon={false}
        labelCol={{ flex: 'none' }}
        wrapperCol={{ flex: 'auto' }}
        onFinish={handleFinish}
        disabled={create.isPending}
        // Form dựng lại mỗi lần mở (destroyOnHidden) nên giá trị mặc định đặt ở đây, không dùng setFieldsValue trước khi dựng.
        initialValues={{ warehouseType: WAREHOUSE_TYPE.MATERIAL, issueDate: dayjs() }}
      >
        <Form.Item name="warehouseType" style={ISSUE_FIELD}>
          <Radio.Group
            optionType="button"
            buttonStyle="solid"
            onChange={() => {
              form.setFieldsValue(RESET_TARGET);
              fromRequest.clear();
            }}
            options={[
              { value: WAREHOUSE_TYPE.MATERIAL, label: LABELS.issueFabric },
              { value: WAREHOUSE_TYPE.SEMI_FINISHED, label: LABELS.issuePieces },
            ]}
          />
        </Form.Item>
        <IssueTargetFields
          isMaterial={isMaterial}
          request={request}
          workOrders={selectable}
          workOrdersLoading={workOrders.isLoading}
          teams={teams}
          teamLocked={teamLocked}
          orderLocked={orderLocked}
          onRequestChange={(id) => void fromRequest.choose(id)}
          onWorkOrderChange={handleWorkOrderChange}
          onOrderChange={() => !isMaterial && form.setFieldValue('lines', [])}
        />
        {isMaterial ? (
          <MaterialIssueLines
            form={form}
            workOrderId={workOrderId}
            orderId={orderId}
            seedFromWorkOrder={!request}
            requested={requestedQtyMap(request, true)}
          />
        ) : (
          <PieceIssueLines orderId={orderId} requested={requestedQtyMap(request, false)} />
        )}
      </Form>
    </Modal>
  );
}
