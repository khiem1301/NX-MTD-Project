import { Button, Card, Flex, Space, Tag, Tooltip, Typography, theme } from 'antd';
import {
  CheckCircleFilled,
  CloseOutlined,
  EditOutlined,
  FileExcelOutlined,
} from '@ant-design/icons';
import { EXCEL_ICON_COLOR } from '../constants';
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
import { ExcelReviewModal } from './ExcelReviewModal';
import { ExcelUploadButton } from './ExcelUploadButton';
import { HintTip } from '@/components/common/HintTip';

const text = MESSAGES.orders.productExcel;
const SPEC_KINDS: readonly ProductImportKind[] = ['components', 'norms', 'groups'];
/** Ba ô Định mức chia 3 cột (lưới 24). */
const SPEC_SPAN = 8;

type ProductExcelImportsProps = {
  /** Mã hàng đang sửa; undefined = đang thêm mới. */
  productId?: number;
  value: ProductImportSelection;
  onChange: (next: ProductImportSelection) => void;
  disabled?: boolean;
  /** Mở từ form đơn hàng: dữ liệu được nhập sau khi Lưu đơn (đơn tạo màu, size). */
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

  /** Ô trạng thái từng mục: chỉ hiện số dòng đã đọc từ file chung, không có nút chọn file riêng. */
  const slots = (kinds: readonly ProductImportKind[], span: number) => (
    <div
      style={{
        display: 'grid',
        gridTemplateColumns: `repeat(auto-fit, minmax(${span * 24}px, 1fr))`,
        gap: token.marginSM,
      }}
    >
      {kinds.map((kind) => {
        const rows = rowsOfKind(kind);
        const isRequired = required.includes(kind);
        const missing = showMissing && isRequired && rows === 0;
        const accent = rows
          ? EXCEL_ICON_COLOR
          : missing
            ? token.colorError
            : token.colorTextQuaternary;
        return (
          <div key={kind} style={{ minWidth: 0 }}>
            <Flex
              align="center"
              gap={token.marginSM}
              style={{
                padding: `${token.paddingXS}px ${token.paddingSM}px`,
                borderRadius: token.borderRadiusLG,
                border: `1px solid ${missing ? token.colorError : token.colorBorderSecondary}`,
                borderLeft: `4px solid ${accent}`,
                background: token.colorFillAlter,
              }}
            >
              {rows ? (
                <CheckCircleFilled style={{ fontSize: 18, color: accent }} />
              ) : (
                <FileExcelOutlined style={{ fontSize: 18, color: accent }} />
              )}
              <div style={{ flex: 1, minWidth: 0, lineHeight: 1.3 }}>
                <Typography.Text strong>
                  {isRequired && <span style={{ color: token.colorError }}>* </span>}
                  {text.kinds[kind]}
                </Typography.Text>
                <div>
                  <Typography.Text
                    type={missing ? 'danger' : rows ? undefined : 'secondary'}
                    style={{ fontSize: token.fontSizeSM }}
                  >
                    {rows ? fill(text.rowsReady, rows) : missing ? text.slotRequired : text.noData}
                  </Typography.Text>
                </div>
              </div>
              {rows > 0 && (
                <Button
                  size="small"
                  type="text"
                  aria-label={`${MESSAGES.common.delete} ${text.kinds[kind]}`}
                  icon={<CloseOutlined />}
                  disabled={disabled}
                  onClick={() => bundle && onChange({ ...value, bundle: clearKind(bundle, kind) })}
                />
              )}
            </Flex>
          </div>
        );
      })}
    </div>
  );

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
            <ImportTemplateLink kind="product-form" productId={productId} />
          </Space>
        }
      >
        <Flex vertical gap={token.marginXS}>
          <Space size={8} wrap>
            <HintTip text={deferred ? text.hintDeferred : text.hint} />
            {required.length > 0 && (
              <Typography.Text type="danger">
                {fill(text.requiredHint, required.map((k) => text.kinds[k]).join(', '))}
              </Typography.Text>
            )}
          </Space>
          {slots(SPEC_KINDS, SPEC_SPAN)}
        </Flex>
      </Card>
      {imports.reviewing && (
        <ExcelReviewModal
          fileName={imports.reviewing.fileName}
          bundle={imports.reviewing.bundle}
          productId={productId ?? null}
          only={imports.reviewing.kind ? [imports.reviewing.kind] : undefined}
          allowReplace={productId !== undefined}
          initialReplace={value.replace === true}
          onConfirm={imports.confirm}
          onCancel={imports.closeReview}
        />
      )}
    </>
  );
}
