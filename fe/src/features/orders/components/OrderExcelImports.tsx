import { Col, Flex, Row, Space, Typography } from 'antd';
import { ImportTemplateLink } from '@/components/common/ImportTemplateLink';
import { MESSAGES } from '@/constants/messages';
import type { useOrderExcelImports } from '../hooks/useOrderExcelImports';
import { orderRowCount, type OrderImportKind } from '../utils/orderExcelImports';
import { ExcelKindSlot } from './ExcelKindSlot';
import { ExcelReviewModal } from './ExcelReviewModal';

const text = MESSAGES.orders.orderExcel;
const kinds = MESSAGES.orders.productExcel.kinds;
const KINDS: readonly OrderImportKind[] = ['markers', 'orderMaterials'];

type OrderExcelImportsProps = {
  orderId: number | null;
  imports: ReturnType<typeof useOrderExcelImports>;
  disabled?: boolean;
};

/** DEV-ALL-150/154: khung nhập Bìa của đơn, NVL của đơn ở form đơn — chọn file mở popup kiểm tra / sửa rồi Xác nhận. */
export function OrderExcelImports({ orderId, imports, disabled }: OrderExcelImportsProps) {
  const { selection, reading, reviewing } = imports;
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
              onFile={(file) => imports.choose(kind, file)}
              onClear={() => imports.clear(kind)}
              onReview={
                orderRowCount(selection, kind) > 0 ? () => imports.reviewAgain(kind) : undefined
              }
            />
          </Col>
        ))}
      </Row>
      {reviewing && (
        <ExcelReviewModal
          fileName={reviewing.fileName}
          bundle={reviewing.bundle}
          productId={null}
          orderId={orderId}
          only={imports.onlyTables(reviewing.kind)}
          onConfirm={imports.confirm}
          onCancel={imports.closeReview}
        />
      )}
    </div>
  );
}
