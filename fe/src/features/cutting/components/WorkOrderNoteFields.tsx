import { Col, Form, Input, Row } from 'antd';
import { QtyInput } from '@/components/common/QtyInput';
import { LABELS, WORK_ORDER_LIMITS } from '../constants';

/** Tỉ lệ sai lệch vải (trần đơn xin NVL theo lệnh, DEV-ALL-92) và ghi chú của lệnh cắt. */
export function WorkOrderNoteFields() {
  return (
    <Row gutter={12} style={{ marginTop: 12 }}>
      <Col xs={24} sm={8} lg={6}>
        <Form.Item
          name="fabricTolerancePercent"
          label={LABELS.fabricTolerance}
          tooltip={LABELS.fabricToleranceHint}
          initialValue={0}
          style={{ marginBottom: 0 }}
        >
          <QtyInput
            min={0}
            max={WORK_ORDER_LIMITS.maxFabricTolerance}
            decimals={WORK_ORDER_LIMITS.fabricToleranceDecimals}
            style={{ width: '100%' }}
            addonAfter="%"
          />
        </Form.Item>
      </Col>
      <Col xs={24} sm={16} lg={18}>
        <Form.Item name="note" label={LABELS.note} style={{ marginBottom: 0 }}>
          <Input.TextArea
            autoSize={{ minRows: 1, maxRows: 3 }}
            maxLength={WORK_ORDER_LIMITS.noteMax}
            showCount
          />
        </Form.Item>
      </Col>
    </Row>
  );
}
