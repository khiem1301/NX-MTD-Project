import { PlusOutlined } from '@ant-design/icons';
import { Col, Form, Input, Modal, Row, Select, Upload, message } from 'antd';
import dayjs from 'dayjs';
import { ATTACHMENT_ACCEPT, MAX_ATTACHMENT_BYTES } from '@/constants/attachments';
import { ROLE_LABEL } from '@/constants/hr';
import { MESSAGES } from '@/constants/messages';
import { fill } from '@/utils/format';
import type { RequestDetail } from '@/types/request';
import { AppDatePicker } from '@/components/common/DateInputs';
import { CUTTING_TEAM_TYPE, NO_LINE_TYPES } from '../constants';
import { RequestLineFields } from './RequestLineFields';
import { WorkOrderRequisitionField } from './WorkOrderRequisitionField';
import { useWorkOrderAllowance } from '../hooks/useWorkOrderRequisition';
import { useAuth } from '@/hooks/useAuth';
import { useRequestForm } from '../hooks/useRequestForm';
import type { RequestType } from '@/constants/requestTypes';

export type RequestFormModalProps = {
  open: boolean;
  editing?: RequestDetail;
  maxPhotos: number;
  onClose: () => void;
  onSaved: (requestId: number) => void;
};

const required = { required: true, message: MESSAGES.common.required };

/** Form chung của các loại đơn tạo tay: chọn loại, người xử lý (đơn Khác), tiêu đề, nội dung, ngày cần, dòng, ảnh. */
export function RequestFormModal({
  open,
  editing,
  maxPhotos,
  onClose,
  onSaved,
}: RequestFormModalProps) {
  const {
    create,
    update,
    options,
    changeType,
    handleFinish,
    initialValues,
    form,
    files,
    setFiles,
  } = useRequestForm({ editing, onSaved });
  const requestType = Form.useWatch('requestType', form) as RequestType | undefined;
  const noLines = requestType !== undefined && NO_LINE_TYPES.includes(requestType);
  const handlerRoles = options.find((o) => o.requestType === requestType)?.handlerRoleOptions;
  const { user } = useAuth();
  const byWorkOrder =
    requestType === 'MaterialRequisition' &&
    (editing ? editing.workOrderId !== null : user?.teamType === CUTTING_TEAM_TYPE);
  const workOrderId = Form.useWatch('workOrderId', form) as number | undefined;
  const allowance = useWorkOrderAllowance(
    byWorkOrder ? workOrderId : undefined,
    editing?.requestId,
  );

  return (
    <Modal
      open={open}
      title={editing ? MESSAGES.requests.edit : MESSAGES.requests.create}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={create.isPending || update.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
      width={760}
    >
      <Form
        form={form}
        layout="vertical"
        preserve={false}
        onFinish={handleFinish}
        initialValues={initialValues}
      >
        <Row gutter={12}>
          <Col xs={24} sm={noLines && !editing ? 12 : 24}>
            {/* Ô chọn không gắn thẳng vào form để hỏi trước khi đổi loại (xóa dòng đã nhập). */}
            <Form.Item label={MESSAGES.requests.type} required>
              <Select
                value={requestType}
                disabled={!!editing}
                placeholder={MESSAGES.requests.typePlaceholder}
                aria-label={MESSAGES.requests.type}
                onChange={changeType}
                options={options.map((o) => ({ value: o.requestType, label: o.label }))}
              />
              <Form.Item name="requestType" noStyle rules={[required]}>
                <Input type="hidden" />
              </Form.Item>
            </Form.Item>
          </Col>
          {noLines && !editing && (
            <Col xs={24} sm={12}>
              <Form.Item
                name="handlerRole"
                label={MESSAGES.requests.handlerRole}
                rules={[required]}
              >
                <Select
                  placeholder={MESSAGES.requests.handlerRolePlaceholder}
                  aria-label={MESSAGES.requests.handlerRole}
                  options={(handlerRoles ?? []).map((r) => ({ value: r, label: ROLE_LABEL[r] }))}
                />
              </Form.Item>
            </Col>
          )}
        </Row>
        <Row gutter={12}>
          <Col xs={24} sm={16}>
            <Form.Item name="title" label={MESSAGES.requests.titleField} rules={[{ max: 200 }]}>
              <Input />
            </Form.Item>
          </Col>
          <Col xs={24} sm={8}>
            <Form.Item name="targetDate" label={MESSAGES.requests.targetDate}>
              <AppDatePicker
                style={{ width: '100%' }}
                disabledDate={(d) => d.isBefore(dayjs(), 'day')}
              />
            </Form.Item>
          </Col>
        </Row>
        <Form.Item
          name="content"
          label={MESSAGES.requests.content}
          rules={[{ ...required, whitespace: true }, { max: 2000 }]}
        >
          <Input.TextArea rows={3} placeholder={MESSAGES.requests.contentPlaceholder} />
        </Form.Item>
        {byWorkOrder && (
          <WorkOrderRequisitionField form={form} allowance={allowance.data} locked={!!editing} />
        )}
        {requestType && !noLines && (
          <RequestLineFields
            form={form}
            requestType={requestType}
            allowance={byWorkOrder && workOrderId ? allowance.data : undefined}
          />
        )}
        {!editing && (
          <Form.Item label={fill(MESSAGES.requests.photos, maxPhotos)} style={{ marginTop: 16 }}>
            <Upload
              listType="picture-card"
              accept={ATTACHMENT_ACCEPT}
              fileList={files}
              beforeUpload={(f) => {
                if (f.size > MAX_ATTACHMENT_BYTES) {
                  void message.error(MESSAGES.attachments.tooLarge);
                  return Upload.LIST_IGNORE;
                }
                return false; // giữ file ở client, tải sau khi tạo đơn
              }}
              onChange={({ fileList }) => setFiles(fileList.slice(0, maxPhotos))}
            >
              {files.length < maxPhotos && (
                <div>
                  <PlusOutlined />
                  <div style={{ marginTop: 4 }}>{MESSAGES.attachments.add}</div>
                </div>
              )}
            </Upload>
          </Form.Item>
        )}
      </Form>
    </Modal>
  );
}
