import { Button, Flex, Typography, theme } from 'antd';
import { CloseOutlined, EditOutlined, FileExcelOutlined } from '@ant-design/icons';
import { MESSAGES } from '@/constants/messages';
import { fill } from '@/utils/format';
import { EXCEL_ICON_COLOR } from '../constants';
import { ExcelUploadButton } from './ExcelUploadButton';

const text = MESSAGES.orders.productExcel;

type ExcelKindSlotProps = {
  label: string;
  /** Số dòng đã kiểm (0 = chưa chọn file). */
  rows: number;
  disabled?: boolean;
  loading?: boolean;
  onFile: (file: File) => void;
  onClear: () => void;
  /** DEV-ALL-154: mở lại popup kiểm tra / sửa dữ liệu đã xác nhận. */
  onReview?: () => void;
  /** DEV-ALL-124: mục bắt buộc (dấu * đỏ); missing = đã bấm Lưu mà chưa có dữ liệu (viền đỏ). */
  required?: boolean;
  missing?: boolean;
};

/** DEV-ALL-121/122: một ô mục nhập Excel — số dòng đã kiểm (hoặc "Chưa chọn"), nút chọn file riêng cho mục, nút bỏ. */
export function ExcelKindSlot({
  label,
  rows,
  disabled,
  loading,
  onFile,
  onClear,
  onReview,
  required,
  missing,
}: ExcelKindSlotProps) {
  const { token } = theme.useToken();
  const borderColor = missing ? token.colorError : token.colorBorder;
  return (
    <div
      style={{
        border: `1px ${rows ? 'solid' : 'dashed'} ${borderColor}`,
        borderRadius: token.borderRadiusLG,
        padding: `${token.paddingXS}px ${token.paddingSM}px`,
        height: '100%',
      }}
    >
      <Flex justify="space-between" align="center" gap={8}>
        <Typography.Text strong>
          {required && <span style={{ color: token.colorError }}>* </span>}
          {label}
        </Typography.Text>
        <ExcelUploadButton disabled={disabled} loading={loading} onFile={onFile}>
          {rows ? text.change : text.choose}
        </ExcelUploadButton>
      </Flex>
      <Flex
        align="center"
        gap={4}
        style={{ marginTop: token.marginXS, minHeight: token.controlHeightSM }}
      >
        {rows ? (
          <>
            <FileExcelOutlined style={{ color: EXCEL_ICON_COLOR }} />
            <Typography.Text style={{ flex: 1 }}>{fill(text.rowsReady, rows)}</Typography.Text>
            {onReview && (
              <Button
                size="small"
                type="link"
                icon={<EditOutlined />}
                disabled={disabled}
                onClick={onReview}
              >
                {text.reviewAgain}
              </Button>
            )}
            <Button
              size="small"
              type="text"
              aria-label={`${MESSAGES.common.delete} ${label}`}
              icon={<CloseOutlined />}
              disabled={disabled}
              onClick={onClear}
            />
          </>
        ) : (
          <Typography.Text type={missing ? 'danger' : 'secondary'}>
            {missing ? text.slotRequired : text.noFile}
          </Typography.Text>
        )}
      </Flex>
    </div>
  );
}
