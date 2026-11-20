import { Col, Flex, Row, Space, Typography } from 'antd';
import { ImportTemplateLink } from '@/components/common/ImportTemplateLink';
import { MESSAGES } from '@/constants/messages';
import {
  orderRowCount,
  type OrderImportKind,
  type OrderImportSelection,
} from '../utils/orderExcelImports';
import { ExcelKindSlot } from './ExcelKindSlot';
import { OrderMissingDataAlert } from './OrderMissingDataAlert';

const text = MESSAGES.orders.orderExcel;
const kinds = MESSAGES.orders.productExcel.kinds;
const KINDS: readonly OrderImportKind[] = ['markers', 'orderMaterials'];

type OrderExcelImportsProps = {
  orderId: number | null;
  selection: OrderImportSelection;
  reading: OrderImportKind | undefined;
  /** Đơn đang sửa đã có dữ liệu trong hệ thống (đơn mới: cả hai false). */
  existing: Record<OrderImportKind, boolean>;
  disabled?: boolean;
  onFile: (kind: OrderImportKind, file: File) => void;
  onClear: (kind: OrderImportKind) => void;
};

/** DEV-ALL-150: khung nhập Bìa của đơn, NVL của đơn ở form đơn (không bắt buộc) kèm cảnh báo khi đơn còn thiếu. */
export function OrderExcelImports({
  orderId,
  selection,
  reading,
  existing,
  disabled,
  onFile,
  onClear,
}: OrderExcelImportsProps) {
  const isMissing = (k: OrderImportKind) => !existing[k] && orderRowCount(selection, k) === 0;
  return (
    <div style={{ marginTop: 16 }}>
      <Flex justify="space-between" align="center" wrap gap={8}>
        <Typography.Text strong>{text.section}</Typography.Text>
        <Space size={16} wrap>
          <ImportTemplateLink kind="spec" orderId={orderId} label={text.markerTemplate} />
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
      <OrderMissingDataAlert
        missingMarkers={isMissing('markers')}
        missingMaterials={isMissing('orderMaterials')}
        hint={text.missingFormHint}
      />
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
