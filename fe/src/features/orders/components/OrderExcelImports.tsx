import { Col, Flex, Row, Space, Typography } from 'antd';
import { ImportTemplateLink } from '@/components/common/ImportTemplateLink';
import { MESSAGES } from '@/constants/messages';
import type { useOrderExcelImports } from '../hooks/useOrderExcelImports';
import { orderRowCount, type OrderImportKind } from '../utils/orderExcelImports';
import { ExcelKindSlot } from './ExcelKindSlot';
import { ExcelReviewModal } from './ExcelReviewModal';

const text = MESSAGES.orders.orderExcel;
const kinds = MESSAGES.orders.productExcel.kinds;
// DEV-ALL-192: NVL của đơn đứng trước Bìa của đơn (NVL là bắt buộc khi tạo đơn).
const KINDS: readonly OrderImportKind[] = ['orderMaterials', 'markers'];

type OrderExcelImportsProps = {
  orderId: number | null;
  imports: ReturnType<typeof useOrderExcelImports>;
  disabled?: boolean;
  /** Tạo đơn mới: NVL của đơn bắt buộc (dấu * đỏ). */
  materialsRequired?: boolean;
  /** Đã bấm Lưu mà chưa có file NVL của đơn (viền đỏ). */
  materialsMissing?: boolean;
};

/** DEV-ALL-150/154: khung nhập Bìa của đơn, NVL của đơn ở form đơn — chọn file mở popup kiểm tra / sửa rồi Xác nhận. */
export function OrderExcelImports({
  orderId,
  imports,
  disabled,
  materialsRequired,
  materialsMissing,
}: OrderExcelImportsProps) {
  const { selection, reading, reviewing } = imports;
  return (
    <div style={{ marginTop: 4, marginBottom: 16 }}>
      <Flex justify="space-between" align="center" wrap gap={8}>
        <Typography.Text strong>{text.section}</Typography.Text>
        <Space size={16} wrap>
          <ImportTemplateLink
            kind="order-materials"
            orderId={orderId}
            label={text.materialTemplate}
          />
          <ImportTemplateLink kind="order-markers" orderId={orderId} label={text.markerTemplate} />
        </Space>
      </Flex>
      <Row gutter={[8, 8]} style={{ marginTop: 8 }}>
        {KINDS.map((kind) => (
          <Col key={kind} xs={24} md={12}>
            <ExcelKindSlot
              label={kinds[kind]}
              rows={orderRowCount(selection, kind)}
              disabled={disabled}
              required={kind === 'orderMaterials' && materialsRequired}
              missing={kind === 'orderMaterials' && materialsMissing}
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
