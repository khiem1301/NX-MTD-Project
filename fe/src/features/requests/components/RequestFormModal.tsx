import { PlusOutlined } from '@ant-design/icons';
import { Alert, Form, Input, Modal, Select, Upload, message } from 'antd';
import dayjs from 'dayjs';
import { ATTACHMENT_ACCEPT, MAX_ATTACHMENT_BYTES } from '@/constants/attachments';
import { MESSAGES } from '@/constants/messages';
import { fill } from '@/utils/format';
import type { RequestDetail } from '@/types/request';
import { AppDatePicker } from '@/components/common/DateInputs';
import { RequestLineFields } from './RequestLineFields';
import { useRequestForm } from '../hooks/useRequestForm';
import { EMPTY_LINE } from '../hooks/useRequestForm';

export type RequestFormModalProps = {
  open: boolean;
  editing?: RequestDetail;
  maxPhotos: number;
  onClose: () => void;
  onSaved: (requestId: number) => void;
};

/** Form chung cho 3 loại đơn của Cơ điện: tiêu đề, nội dung, ngày cần, dòng linh kiện + số lượng, ảnh. */
export function RequestFormModal({
  open,
  editing,
  maxPhotos,
  onClose,
  onSaved,
}: RequestFormModalProps) {
  const { create, update, options, handleFinish, initialValues, form, files, setFiles } =
    useRequestForm({ editing, onSaved });
  const requestType = Form.useWatch('requestType', form);

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
      width={640}
    >
      <Form
        form={form}
        layout="vertical"
        preserve={false}
        onFinish={handleFinish}
        initialValues={initialValues}
      >
        <Form.Item
          name="requestType"
          label={MESSAGES.requests.type}
          rules={[{ required: true, message: MESSAGES.common.required }]}
        >
          <Select
            disabled={!!editing}
            onChange={() => form.setFieldValue('lines', [EMPTY_LINE])}
            options={options.map((o) => ({ value: o.requestType, label: o.label }))}
          />
        </Form.Item>
        <Form.Item name="title" label={MESSAGES.requests.titleField} rules={[{ max: 200 }]}>
          <Input />
        </Form.Item>
        <Form.Item
          name="content"
          label={MESSAGES.requests.content}
          rules={[
            { required: true, whitespace: true, message: MESSAGES.common.required },
            { max: 2000 },
          ]}
        >
          <Input.TextArea rows={3} />
        </Form.Item>
        <Form.Item name="targetDate" label={MESSAGES.requests.targetDate}>
          <AppDatePicker disabledDate={(d) => d.isBefore(dayjs(), 'day')} />
        </Form.Item>
        <RequestLineFields form={form} requestType={requestType} />
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
        {editing && (
          <Alert
            type="info"
            showIcon
            message={MESSAGES.attachments.title}
            description={MESSAGES.requests.attachmentsHint}
          />
        )}
      </Form>
    </Modal>
  );
}
