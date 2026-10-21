import { MinusCircleOutlined, PlusOutlined } from '@ant-design/icons';
import {
  Alert,
  Button,
  DatePicker,
  Form,
  Input,
  InputNumber,
  Modal,
  Select,
  Space,
  Upload,
  message,
  type UploadFile,
} from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { useState } from 'react';
import { ATTACHMENT_ACCEPT, MAX_ATTACHMENT_BYTES } from '@/constants/attachments';
import { MESSAGES } from '@/constants/messages';
import type { RequestType } from '@/constants/requestTypes';
import { attachmentApi } from '@/hooks/useAttachments';
import type { ApiError } from '@/types/api';
import { applyFieldErrors } from '@/utils/apiError';
import { fill, toApiDate } from '@/utils/format';
import { GENERIC_FORM_TYPES } from '../constants';
import { useCreateRequest, useRequestTypes, useUpdateRequest } from '../hooks/useRequests';
import type { RequestDetail } from '../types';

type FormValues = {
  requestType: RequestType;
  title?: string;
  content?: string;
  targetDate?: Dayjs;
  lines?: { itemDescription: string; quantity: number }[];
};

type RequestFormModalProps = {
  open: boolean;
  editing?: RequestDetail;
  maxPhotos: number;
  onClose: () => void;
  onSaved: (requestId: number) => void;
};

const EMPTY_LINE = { itemDescription: '', quantity: 1 };

/** Form chung cho 3 loại đơn của Cơ điện: tiêu đề, nội dung, ngày cần, dòng linh kiện + số lượng, ảnh. */
export function RequestFormModal({ open, editing, maxPhotos, onClose, onSaved }: RequestFormModalProps) {
  const [form] = Form.useForm<FormValues>();
  const [files, setFiles] = useState<UploadFile[]>([]);
  const { data: types } = useRequestTypes();
  const create = useCreateRequest();
  const update = useUpdateRequest(editing?.requestId ?? 0);
  const options = (types ?? []).filter((t) => GENERIC_FORM_TYPES.includes(t.requestType));

  // Ảnh cần OwnerId nên chỉ tải được sau khi đơn đã có Id; lỗi một phần không làm mất đơn.
  const uploadPhotos = async (requestId: number) => {
    const results = await Promise.allSettled(
      files.map((f) => attachmentApi.upload('Request', requestId, f.originFileObj as File, 'Photo')),
    );
    const failed = results.filter((r) => r.status === 'rejected').length;
    if (failed > 0) void message.warning(fill(MESSAGES.requests.photoUploadFailed, failed));
  };

  const handleFinish = async (values: FormValues) => {
    const body = {
      title: values.title ?? null,
      content: values.content ?? null,
      targetDate: values.targetDate ? toApiDate(values.targetDate) : null,
      lines: (values.lines ?? []).map((l) => ({ itemDescription: l.itemDescription, quantity: l.quantity })),
    };
    try {
      if (editing) {
        await update.mutateAsync({ ...body, rowVer: editing.rowVer });
        onSaved(editing.requestId);
      } else {
        const newId = await create.mutateAsync({ ...body, requestType: values.requestType });
        await uploadPhotos(newId);
        void message.success(MESSAGES.requests.created);
        onSaved(newId);
      }
      form.resetFields();
      setFiles([]);
    } catch (e) {
      // useApiMutation đã hiện message; lỗi 400 theo field thì gắn thêm vào ô nhập.
      applyFieldErrors(form, e as ApiError);
    }
  };

  const initialValues: Partial<FormValues> = editing
    ? {
        requestType: editing.requestType,
        title: editing.title ?? undefined,
        content: editing.content ?? undefined,
        targetDate: editing.targetDate ? dayjs(editing.targetDate) : undefined,
        lines: editing.lines.map((l) => ({ itemDescription: l.itemDescription ?? '', quantity: l.quantity })),
      }
    : { lines: [EMPTY_LINE] };

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
      <Form form={form} layout="vertical" preserve={false} onFinish={handleFinish} initialValues={initialValues}>
        <Form.Item
          name="requestType"
          label={MESSAGES.requests.type}
          rules={[{ required: true, message: MESSAGES.common.required }]}
        >
          <Select disabled={!!editing} options={options.map((o) => ({ value: o.requestType, label: o.label }))} />
        </Form.Item>
        <Form.Item name="title" label={MESSAGES.requests.titleField} rules={[{ max: 200 }]}>
          <Input />
        </Form.Item>
        <Form.Item
          name="content"
          label={MESSAGES.requests.content}
          rules={[{ required: true, whitespace: true, message: MESSAGES.common.required }, { max: 2000 }]}
        >
          <Input.TextArea rows={3} />
        </Form.Item>
        <Form.Item name="targetDate" label={MESSAGES.requests.targetDate}>
          <DatePicker format="DD/MM/YYYY" disabledDate={(d) => d.isBefore(dayjs(), 'day')} />
        </Form.Item>
        <Form.List name="lines">
          {(fields, { add, remove }) => (
            <>
              {fields.map((f) => (
                <Space key={f.key} align="baseline">
                  <Form.Item
                    name={[f.name, 'itemDescription']}
                    rules={[{ required: true, whitespace: true, message: MESSAGES.common.required }, { max: 300 }]}
                  >
                    <Input placeholder={MESSAGES.requests.itemDescription} style={{ width: 340 }} />
                  </Form.Item>
                  <Form.Item
                    name={[f.name, 'quantity']}
                    rules={[{ required: true, message: MESSAGES.common.required }]}
                  >
                    <InputNumber min={0.001} placeholder={MESSAGES.requests.quantity} />
                  </Form.Item>
                  {fields.length > 1 && (
                    <MinusCircleOutlined aria-label={MESSAGES.common.delete} onClick={() => remove(f.name)} />
                  )}
                </Space>
              ))}
              <Button type="dashed" icon={<PlusOutlined />} onClick={() => add(EMPTY_LINE)}>
                {MESSAGES.requests.addLine}
              </Button>
            </>
          )}
        </Form.List>
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
              {files.length < maxPhotos && <PlusOutlined />}
            </Upload>
          </Form.Item>
        )}
        {editing && <Alert type="info" showIcon message={MESSAGES.attachments.title} description={MESSAGES.requests.attachmentsHint} />}
      </Form>
    </Modal>
  );
}
