import { Button, Form, InputNumber, Select, Space } from 'antd';
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons';
import { MESSAGES } from '@/constants/messages';
import { MAX_PAGE_SIZE } from '@/constants/pagination';
import { useOrderLineOptions } from '@/hooks/useLookups';
import { formatQty } from '@/utils/format';
import { LABELS, WAREHOUSE_TYPE } from '../constants';
import { useBalances } from '../hooks/useInventory';

type PieceIssueLinesProps = { orderId: number | undefined };

/** Dòng cấp BTP cho tổ may theo dòng đơn; hiện số khả dụng trong kho BTP. */
export function PieceIssueLines({ orderId }: PieceIssueLinesProps) {
  const lines = useOrderLineOptions(orderId);
  const balances = useBalances({
    warehouseType: WAREHOUSE_TYPE.SEMI_FINISHED,
    orderId,
    page: 1,
    pageSize: MAX_PAGE_SIZE,
  });
  const available = new Map(
    (balances.data?.items ?? []).map((b) => [b.orderLineId, b.availableQty]),
  );
  const options = (lines.data ?? []).map((l) => ({
    value: l.orderLineId,
    label: `${l.productCode} ${l.color}-${l.sizeCode} (${LABELS.availableQty}: ${formatQty(available.get(l.orderLineId) ?? 0)})`,
  }));
  const required = [{ required: true, message: MESSAGES.common.required }];

  return (
    <Form.List name="lines">
      {(fields, { add, remove }) => (
        <>
          {fields.map((field) => (
            <Space key={field.key} align="start" wrap>
              <Form.Item name={[field.name, 'orderLineId']} rules={required}>
                <Select placeholder={LABELS.orderLine} options={options} style={{ width: 380 }} />
              </Form.Item>
              <Form.Item name={[field.name, 'issuedQty']} rules={required}>
                <InputNumber
                  addonBefore={LABELS.issuedQty}
                  min={1}
                  precision={0}
                  style={{ width: 180 }}
                />
              </Form.Item>
              <Button icon={<DeleteOutlined />} onClick={() => remove(field.name)} />
            </Space>
          ))}
          <Button
            type="dashed"
            icon={<PlusOutlined />}
            onClick={() => add()}
            disabled={orderId === undefined}
          >
            {LABELS.addLine}
          </Button>
        </>
      )}
    </Form.List>
  );
}
