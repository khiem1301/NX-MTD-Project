import { Col, Flex, Row, Space, Typography } from 'antd';
import { ImportTemplateLink } from '@/components/common/ImportTemplateLink';
import { MESSAGES } from '@/constants/messages';
import {
  orderRowCount,
  type OrderImportKind,
  type OrderImportSelection,
} from '../utils/orderExcelImports';
import { ExcelKindSlot } from './ExcelKindSlot';

const text = MESSAGES.orders.orderExcel;
const kinds = MESSAGES.orders.productExcel.kinds;
const KINDS: readonly OrderImportKind[] = ['markers', 'orderMaterials'];

type OrderExcelImportsProps = {
  orderId: number | null;
  selection: OrderImportSelection;
  reading: OrderImportKind | undefined;
  disabled?: boolean;
  onFile: (kind: OrderImportKind, file: File) => void;
  onClear: (kind: OrderImportKind) => void;
};

/** DEV-ALL-150: khung nhập Bìa của đơn, NVL của đơn ở form đơn (không bắt buộc). */
export function OrderExcelImports({
  orderId,
  selection,
  reading,
  disabled,
  onFile,
  onClear,
}: OrderExcelImportsProps) {
  return (
    <div style={{ marginTop: 16 }}>
      <Flex justify="space-between" align="center" wrap gap={8}>
        <Typography.Text strong>{text.section}</Typography.Text>
        <Space size={16} wrap>
          <ImportTemplateLink kind="order-markers" orderId={orderId} label={text.markerTemplate} />
          <ImportTemplateLink
            kind="order-materials"
            orderId={orderId}
            label={text.materialTemplate}
          />
        </Space>
      </Flex>
      <Typography.Paragraph type="secondary" style={{ margin: '4px 0 8px' }}>
        {text.hint} {text.markerTemplateHint}
      </Typography.Paragraph>
      <Row gutter={[8, 8]}>
        {KINDS.map((kind) => (
          <Col key={kind} xs={24} md={12}>
            <ExcelKindSlot
              label={kinds[kind]}
              rows={orderRowCount(selection, kind)}
              disabled={disabled}
              loading={reading === kind}
              onFile={(file) => onFile(kind, file)}
              onClear={() => onClear(kind)}
            />
          </Col>
        ))}
      </Row>
    </div>
  );
}
