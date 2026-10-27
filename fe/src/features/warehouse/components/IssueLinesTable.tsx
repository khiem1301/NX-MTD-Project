import { Form, Input, InputNumber, Table, type TableColumnsType } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { formatQty } from '@/utils/format';
import { LABELS } from '../constants';
import type { MaterialIssueLine } from '../types';

type IssueLinesTableProps = { lines: MaterialIssueLine[]; editable: boolean };

const itemName = (l: MaterialIssueLine): string =>
  l.materialId !== null
    ? `${l.materialCode} ${l.materialName} (${l.materialColor || '—'})`
    : `${l.productCode} ${l.color}-${l.sizeCode}${l.componentName ? ` · ${l.componentName}` : ''}`;

/** Dòng phiếu cấp; editable = tổ trưởng nhập số thực nhận (form field lines[i]). */
export function IssueLinesTable({ lines, editable }: IssueLinesTableProps) {
  const form = Form.useFormInstance();

  const columns: TableColumnsType<MaterialIssueLine> = [
    { title: LABELS.material, key: 'item', render: (_, l) => itemName(l) },
    { title: LABELS.unit, dataIndex: 'unit', render: (v: string | null) => v ?? '—' },
    { title: LABELS.issuedQty, dataIndex: 'issuedQty', align: 'right', render: formatQty },
    {
      title: LABELS.confirmedQty,
      key: 'confirmedQty',
      render: (_, l, index) =>
        editable ? (
          <Form.Item
            name={['lines', index, 'confirmedQty']}
            rules={[{ required: true, message: MESSAGES.common.required }]}
            noStyle
          >
            <InputNumber min={0} max={l.issuedQty} />
          </Form.Item>
        ) : (
          formatQty(l.confirmedQty)
        ),
    },
    {
      title: LABELS.discrepancyReason,
      key: 'reason',
      render: (_, l, index) =>
        editable ? (
          <Form.Item
            name={['lines', index, 'discrepancyReason']}
            dependencies={[['lines', index, 'confirmedQty']]}
            rules={[
              {
                validator: async (__, value?: string) => {
                  const qty = form.getFieldValue(['lines', index, 'confirmedQty']) as
                    number | undefined;
                  if (qty !== l.issuedQty && !value?.trim())
                    throw new Error(MESSAGES.warehouse.discrepancyReasonRequired);
                },
              },
            ]}
            style={{ marginBottom: 0 }}
          >
            <Input maxLength={500} />
          </Form.Item>
        ) : (
          (l.discrepancyReason ?? '—')
        ),
    },
  ];

  return (
    <>
      {editable &&
        lines.map((l, index) => (
          <Form.Item
            key={l.materialIssueLineId}
            name={['lines', index, 'materialIssueLineId']}
            hidden
          >
            <InputNumber />
          </Form.Item>
        ))}
      <Table
        rowKey="materialIssueLineId"
        size="small"
        columns={columns}
        dataSource={lines}
        pagination={false}
      />
    </>
  );
}
