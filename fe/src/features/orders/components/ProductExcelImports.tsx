import { Button, Card, Col, Flex, Row, Space, Tag, Tooltip, Typography, theme } from 'antd';
import { EditOutlined, FileExcelOutlined } from '@ant-design/icons';
import { MESSAGES } from '@/constants/messages';
import { fill } from '@/utils/format';
import { ImportTemplateLink } from '@/components/common/ImportTemplateLink';
import { useProductExcelImports } from '../hooks/useProductExcelImports';
import type { ProductExcelRow } from '../types';
import {
  clearKind,
  rowCountOf,
  type ProductImportKind,
  type ProductImportSelection,
} from '../utils/productExcelImports';
import { ExcelKindSlot } from './ExcelKindSlot';
import { ExcelReviewModal } from './ExcelReviewModal';
import { ExcelUploadButton } from './ExcelUploadButton';

const text = MESSAGES.orders.productExcel;
const SPEC_KINDS: readonly ProductImportKind[] = ['components', 'norms', 'groups'];
const ORDER_KINDS: readonly ProductImportKind[] = ['markers', 'orderMaterials'];
/** Ô Định mức chia 3 cột, ô Theo đơn hàng chia 2 cột (lưới 24). */
const SPEC_SPAN = 8;
const ORDER_SPAN = 12;

type ProductExcelImportsProps = {
  /** Mã hàng đang sửa; undefined = đang thêm mới (chưa có đơn để chọn). */
  productId?: number;
  value: ProductImportSelection;
  onChange: (next: ProductImportSelection) => void;
  disabled?: boolean;
  /** Mở từ form đơn hàng: dữ liệu được nhập vào chính đơn đó sau khi Lưu đơn (không cần chọn đơn). */
  deferred?: boolean;
  /** Xác nhận popup của file chung có sheet Mã hàng: form điền Mã hàng, Tên, Mô tả từ dòng này. */
  onProductRow?: (row: ProductExcelRow) => void;
  /** Mở popup kiểm tra ngay khi gắn (dữ liệu đã đọc sẵn, vd từ hộp Nhập dòng hàng của form đơn). */
  autoReview?: boolean;
  /** DEV-ALL-124: mục bắt buộc phải có dữ liệu trước khi Lưu (Thêm mới). */
  required?: readonly ProductImportKind[];
  /** Đã bấm Lưu mà còn thiếu: tô đỏ các ô bắt buộc chưa có dữ liệu. */
  showMissing?: boolean;
};

function GroupTitle({ title, note }: { title: string; note: string }) {
  return (
    <div>
      <Typography.Text strong>{title}</Typography.Text>
      <Typography.Text type="secondary"> · {note}</Typography.Text>
    </div>
  );
}

/**
 * DEV-ALL-121/122: khung "Nhập từ Excel" của form mã hàng. Chọn file (chung nhiều sheet, hoặc riêng từng mục) thì đọc file rồi mở
 * popup kiểm tra để sửa và Xác nhận; khung chỉ giữ dữ liệu đã xác nhận. Việc ghi chạy sau khi Lưu mã hàng (hoặc Lưu đơn).
 */
export function ProductExcelImports({
  productId,
  value,
  onChange,
  disabled,
  deferred = false,
  onProductRow,
  autoReview = false,
  required = [],
  showMissing = false,
}: ProductExcelImportsProps) {
  const { token } = theme.useToken();
  const imports = useProductExcelImports({ value, onChange, onProductRow, autoReview });
  const { bundle, fileName, newMaterials = [] } = value;
  const rowsOfKind = (kind: ProductImportKind) => (bundle ? rowCountOf(bundle, kind) : 0);

  const slots = (kinds: readonly ProductImportKind[], span: number) => (
    <Row gutter={[token.marginSM, token.marginSM]}>
      {kinds.map((kind) => (
        <Col key={kind} xs={24} md={span}>
          <ExcelKindSlot
            label={text.kinds[kind]}
            rows={rowsOfKind(kind)}
            disabled={disabled}
            loading={imports.parsing(kind)}
            onFile={(file) => imports.openFile(file, kind)}
            onClear={() => bundle && onChange({ ...value, bundle: clearKind(bundle, kind) })}
            required={required.includes(kind)}
            missing={showMissing && required.includes(kind) && rowsOfKind(kind) === 0}
          />
        </Col>
      ))}
    </Row>
  );

  /** Chỉ form đơn hàng (deferred) có Bìa cắt, NVL của đơn: nhập vào chính đơn đó sau khi Lưu đơn. Form mã hàng không có phần theo đơn. */
  const orderGroup = deferred ? (
    <>
      <GroupTitle title={text.orderGroup} note={text.orderGroupDeferred} />
      {slots(ORDER_KINDS, ORDER_SPAN)}
    </>
  ) : null;

  return (
    <>
      <Card
        size="small"
        style={{ marginTop: token.margin }}
        title={
          <Space size={8} wrap>
            <span>{text.section}</span>
            {fileName && bundle && <Tag icon={<FileExcelOutlined />}>{fileName}</Tag>}
            {newMaterials.length > 0 && (
              <Tag color="orange">{fill(text.newMaterialsTag, newMaterials.length)}</Tag>
            )}
          </Space>
        }
        extra={
          <Space size={12} wrap>
            {bundle && fileName && (
              <Button
                size="small"
                icon={<EditOutlined />}
                disabled={disabled}
                onClick={imports.review}
              >
                {text.reviewAgain}
              </Button>
            )}
            <Tooltip title={text.uploadAllTip}>
              <span>
                <ExcelUploadButton
                  primary
                  disabled={disabled}
                  loading={imports.parsing('all')}
                  onFile={(file) => imports.openFile(file)}
                >
                  {text.uploadAll}
                </ExcelUploadButton>
              </span>
            </Tooltip>
            <ImportTemplateLink
              kind="product-form"
              productId={productId}
              orderId={value.orderId}
              withOrder={deferred}
            />
          </Space>
        }
      >
        <Typography.Paragraph type="secondary" style={{ marginBottom: token.marginSM }}>
          {deferred ? text.hintDeferred : text.hint}{' '}
          {required.length > 0 && (
            <Typography.Text type="danger">
              {fill(text.requiredHint, required.map((k) => text.kinds[k]).join(', '))}
            </Typography.Text>
          )}
        </Typography.Paragraph>
        <Flex vertical gap={8}>
          <GroupTitle
            title={text.specGroup}
            note={deferred ? text.specGroupNoteDeferred : text.specGroupNote}
          />
          {slots(SPEC_KINDS, SPEC_SPAN)}
        </Flex>
        {orderGroup && (
          <Flex vertical gap={8} style={{ marginTop: token.margin }}>
            {orderGroup}
          </Flex>
        )}
      </Card>
      {imports.reviewing && (
        <ExcelReviewModal
          fileName={imports.reviewing.fileName}
          bundle={imports.reviewing.bundle}
          productId={productId ?? null}
          orderId={value.orderId ?? null}
          only={imports.reviewing.kind ? [imports.reviewing.kind] : undefined}
          onConfirm={imports.confirm}
          onCancel={imports.closeReview}
        />
      )}
    </>
  );
}
