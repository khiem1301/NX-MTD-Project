import { Form, Input, InputNumber, Modal, Table } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { DISPATCH_LABELS } from '../constants';
import { usePrepareDispatch } from '../hooks/useDispatch';
import type { DispatchNote, DispatchNoteLine } from '../types';

type Props = { note: DispatchNote; open: boolean; onClose: () => void };
type FormValues = { qty: Record<string, number>; note?: string };

/**
 * Thủ kho lập phiếu xuất: chốt số xuất thực tế từng dòng (từ 1 đến số Quản đốc đã đặt), rồi gửi Quản đốc duyệt.
 * Giảm số xuất thì phần giữ chỗ thừa được nhả.
 */
export function PrepareDispatchModal({ note, open, onClose }: Props) {
  const [form] = Form.useForm<FormValues>();
  const prepare = usePrepareDispatch();

  const handleFinish = (v: FormValues) =>
    prepare.mutate(
      {
        id: note.dispatchNoteId,
        body: {
          lines: note.lines.map((l) => ({ dispatchNoteLineId: l.dispatchNoteLineId, issueQty: v.qty[String(l.dispatchNoteLineId)] ?? l.issueQty })),
          note: v.note?.trim() || null,
        },
      },
      { onSuccess: onClose },
    );

  return (
    <Modal
      open={open}
      title={DISPATCH_LABELS.prepareTitle}
      okText={DISPATCH_LABELS.prepareSubmit}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={prepare.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      width={640}
      destroyOnHidden
    >
      <p>{DISPATCH_LABELS.prepareHint}</p>
      <Form
        form={form}
        layout="vertical"
        disabled={prepare.isPending}
        initialValues={{ qty: Object.fromEntries(note.lines.map((l) => [String(l.dispatchNoteLineId), l.issueQty])) }}
        onFinish={handleFinish}
      >
        <Table<DispatchNoteLine>
          rowKey="dispatchNoteLineId"
          size="small"
          pagination={false}
          dataSource={note.lines}
          columns={[
            { title: DISPATCH_LABELS.orderLine, key: 'line', render: (_, l) => `${l.productCode} - ${l.color} - ${l.sizeCode}` },
            { title: DISPATCH_LABELS.requestedQty, dataIndex: 'issueQty', align: 'right' },
            {
              title: DISPATCH_LABELS.actualIssueQty,
              key: 'qty',
              align: 'right',
              render: (_, l) => (
                <Form.Item
                  name={['qty', String(l.dispatchNoteLineId)]}
                  noStyle
                  rules={[{ required: true, type: 'number', min: 1, max: l.issueQty, message: DISPATCH_LABELS.qtyRange.replace('{max}', String(l.issueQty)) }]}
                >
                  <InputNumber min={1} max={l.issueQty} precision={0} />
                </Form.Item>
              ),
            },
          ]}
        />
        <Form.Item name="note" label={DISPATCH_LABELS.note} style={{ marginTop: 16 }}>
          <Input.TextArea rows={2} maxLength={500} showCount />
        </Form.Item>
      </Form>
    </Modal>
  );
}
