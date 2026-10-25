import { useState } from 'react';
import { Alert, DatePicker, Descriptions, Form, Input, Modal, Select, Table } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { MESSAGES } from '@/constants/messages';
import { useTeamOptions } from '@/hooks/useLookups';
import { LABELS } from '../constants';
import { useCreateReplacement, useReplacementRequests } from '../hooks/useCutting';

type ReplacementWorkOrderModalProps = { open: boolean; onClose: () => void };
type FormValues = { requestId: number; cuttingTeamId: number; dueDate: Dayjs; note?: string };

const CUTTING_TEAM_TYPE = 'Cutting';
const API_DATE = 'YYYY-MM-DD';

/** UC19 (TH-10): Quản đốc lập lệnh cắt bù từ đơn cắt bù đã duyệt; dòng lệnh lấy từ đơn (chi tiết + số lượng). */
export function ReplacementWorkOrderModal({ open, onClose }: ReplacementWorkOrderModalProps) {
  const [form] = Form.useForm<FormValues>();
  const requests = useReplacementRequests(open);
  const teams = useTeamOptions(CUTTING_TEAM_TYPE);
  const create = useCreateReplacement();
  const [requestId, setRequestId] = useState<number>();
  const selected = (requests.data ?? []).find((r) => r.requestId === requestId);
  const required = [{ required: true, message: MESSAGES.common.required }];

  return (
    <Modal
      open={open}
      width={720}
      title={LABELS.createReplacement}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={create.isPending}
      okButtonProps={{ disabled: !selected }}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      {!requests.isLoading && (requests.data ?? []).length === 0 && (
        <Alert
          type="info"
          showIcon
          message={LABELS.noReplacementRequest}
          style={{ marginBottom: 12 }}
        />
      )}
      <Form
        form={form}
        layout="vertical"
        disabled={create.isPending}
        initialValues={{ dueDate: dayjs().add(3, 'day') }}
        onFinish={(v) =>
          create.mutate(
            {
              requestId: v.requestId,
              cuttingTeamId: v.cuttingTeamId,
              dueDate: v.dueDate.format(API_DATE),
              note: v.note,
            },
            { onSuccess: onClose },
          )
        }
      >
        <Form.Item name="requestId" label={LABELS.replacementRequest} rules={required}>
          <Select
            placeholder={LABELS.selectReplacementRequest}
            loading={requests.isLoading}
            onChange={(id: number) => setRequestId(id)}
            options={(requests.data ?? []).map((r) => ({
              value: r.requestId,
              label: `${r.requestCode} · ${r.orderCode} · ${r.productCode}`,
            }))}
          />
        </Form.Item>
        {selected && (
          <>
            <Descriptions column={1} size="small" bordered style={{ marginBottom: 12 }}>
              <Descriptions.Item label={LABELS.order}>{selected.orderCode}</Descriptions.Item>
              {selected.content && (
                <Descriptions.Item label={LABELS.note}>{selected.content}</Descriptions.Item>
              )}
            </Descriptions>
            <Table
              rowKey={(l) => `${l.orderLineId}-${l.productComponentId}`}
              size="small"
              pagination={false}
              dataSource={selected.lines}
              style={{ marginBottom: 12 }}
              columns={[
                {
                  title: LABELS.orderLine,
                  key: 'line',
                  render: (_, l) => `${l.color} - ${l.sizeCode}`,
                },
                { title: LABELS.panelName, dataIndex: 'componentName' },
                { title: LABELS.plannedQty, dataIndex: 'quantity', align: 'right' },
              ]}
            />
          </>
        )}
        <Form.Item name="cuttingTeamId" label={LABELS.cuttingTeam} rules={required}>
          <Select
            loading={teams.isLoading}
            options={(teams.data ?? []).map((t) => ({ value: t.teamId, label: t.teamName }))}
          />
        </Form.Item>
        <Form.Item name="dueDate" label={LABELS.dueDate} rules={required}>
          <DatePicker format="DD/MM/YYYY" />
        </Form.Item>
        <Form.Item name="note" label={LABELS.note}>
          <Input.TextArea rows={2} maxLength={500} />
        </Form.Item>
      </Form>
    </Modal>
  );
}
