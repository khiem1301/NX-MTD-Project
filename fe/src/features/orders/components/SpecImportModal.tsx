import { Alert, Modal, Select, Space, Spin, Upload } from 'antd';
import { InboxOutlined } from '@ant-design/icons';
import { ImportTemplateLink } from '@/components/common/ImportTemplateLink';
import { MESSAGES } from '@/constants/messages';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { fill, formatDate } from '@/utils/format';
import { useProductOrders } from '../hooks/useSpecImport';
import { useSpecImportModal, type SpecImportSource } from '../hooks/useSpecImportModal';
import { ExcelReviewModal } from './ExcelReviewModal';
import { HintTip } from '@/components/common/HintTip';

const text = MESSAGES.orders.specImport;
const SELECT_WIDTH = 320;

type SpecImportModalProps = {
  source: SpecImportSource;
  onClose: () => void;
};

/** Màn mã hàng: chọn đơn đang chạy có mã hàng này để nhập kèm bìa của đơn (bỏ trống = chỉ định mức). */
function OrderPicker({
  productId,
  value,
  onChange,
}: {
  productId: number;
  value: number | undefined;
  onChange: (orderId: number | undefined) => void;
}) {
  const orders = useProductOrders(productId);
  return (
    <Select
      allowClear
      showSearch
      optionFilterProp="label"
      style={{ width: SELECT_WIDTH }}
      placeholder={text.orderPlaceholder}
      notFoundContent={text.noOrders}
      loading={orders.isLoading}
      value={value}
      onChange={onChange}
      options={(orders.data?.items ?? []).map((o) => ({
        value: o.orderId,
        label: fill(text.orderOption, o.orderCode, formatDate(o.dueDate)),
      }))}
    />
  );
}

/**
 * DEV-ALL-115/175: nhập Excel định mức mã hàng (thành phần, định mức NVL, nhóm bìa) và bìa của đơn. Chọn file là mở popup kiểm tra
 * (như form mã hàng): sửa từng ô, máy chủ kiểm lại sau mỗi lần sửa; Xác nhận thì lưu cả định mức và bìa một lần.
 * Chỉ gắn khi đang mở nên trạng thái tự reset mỗi lần mở.
 */
export function SpecImportModal({ source, onClose }: SpecImportModalProps) {
  const m = useSpecImportModal(source, onClose);
  const title =
    source.scope === 'admin'
      ? fill(text.title, source.productLabel)
      : fill(text.title, source.orderCode);

  return (
    <Modal
      open
      width={MODAL_WIDTH.xl}
      title={title}
      footer={null}
      onCancel={onClose}
      destroyOnHidden
    >
      <Space direction="vertical" size={16} style={{ width: '100%' }}>
        <HintTip text={source.scope === 'manager' ? text.markersHint : text.hint} />
        <Space wrap size={24}>
          {source.scope === 'admin' ? (
            <Space>
              <span>{text.order}</span>
              <OrderPicker
                productId={source.productId}
                value={m.orderId}
                onChange={m.chooseOrder}
              />
            </Space>
          ) : (
            <Space>
              <span>{text.product}</span>
              <Select
                style={{ width: SELECT_WIDTH }}
                placeholder={text.productPlaceholder}
                value={m.productId}
                onChange={m.chooseProduct}
                options={source.products.map((p) => ({
                  value: p.productId,
                  label: `${p.productCode}`,
                }))}
              />
            </Space>
          )}
          <ImportTemplateLink kind="spec" productId={m.productId} orderId={m.orderId} />
        </Space>

        <Upload.Dragger
          maxCount={1}
          accept=".xlsx"
          disabled={m.parsing || m.importing}
          showUploadList={false}
          beforeUpload={(file) => {
            m.chooseFile(file);
            return false;
          }}
        >
          <p className="ant-upload-drag-icon">
            <InboxOutlined />
          </p>
          <p>{text.chooseFile}</p>
          <p className="ant-upload-hint">{text.chooseFileHint}</p>
        </Upload.Dragger>

        {(m.parsing || m.importing) && <Spin />}
        {m.parseError && <Alert type="error" showIcon message={m.parseError} />}
      </Space>
      {m.reviewing && m.productId !== undefined && (
        <ExcelReviewModal
          fileName={m.reviewing.fileName}
          bundle={m.reviewing.bundle}
          productId={m.productId}
          orderId={m.orderId ?? null}
          only={m.reviewTables}
          allowReplace
          scope={m.scope}
          onConfirm={(bundle, materials, replace) => void m.confirm(bundle, materials, replace)}
          onCancel={m.closeReview}
        />
      )}
    </Modal>
  );
}
