import { Alert, Modal, Select, Space, Spin, Typography, Upload } from 'antd';
import { DownloadOutlined, InboxOutlined } from '@ant-design/icons';
import { MESSAGES } from '@/constants/messages';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { fill, formatDate } from '@/utils/format';
import { SPEC_IMPORT_TEMPLATE_URL } from '../constants';
import { useProductOrders } from '../hooks/useSpecImport';
import { useSpecImportModal, type SpecImportSource } from '../hooks/useSpecImportModal';
import { SpecImportPreviewTabs } from './SpecImportPreviewTabs';

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
 * DEV-ALL-115: nhập Excel định mức mã hàng (thành phần, định mức NVL, nhóm bìa) và bìa của đơn trong một hộp thoại. Chọn file là
 * xem trước ngay (kiểm từng dòng + mọi kiểm tra của nút Lưu trên bản đã gộp); bấm Nhập thì lưu cả định mức và bìa một lần.
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
      okText={text.confirm}
      cancelText={MESSAGES.common.cancel}
      okButtonProps={{ disabled: !m.canImport, loading: m.doImport.isPending }}
      onOk={m.handleConfirm}
      onCancel={onClose}
      destroyOnHidden
    >
      <Space direction="vertical" size={16} style={{ width: '100%' }}>
        <Typography.Text type="secondary">{text.hint}</Typography.Text>
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
                  label: `${p.productCode} - ${p.productName}`,
                }))}
              />
            </Space>
          )}
          <Typography.Link href={SPEC_IMPORT_TEMPLATE_URL} download>
            <DownloadOutlined /> {text.template}
          </Typography.Link>
        </Space>

        <Upload.Dragger
          maxCount={1}
          accept=".xlsx"
          disabled={m.preview.isPending}
          beforeUpload={(file) => {
            m.chooseFile(file);
            return false;
          }}
          onRemove={() => m.chooseFile(null)}
        >
          <p className="ant-upload-drag-icon">
            <InboxOutlined />
          </p>
          <p>{text.chooseFile}</p>
          <p className="ant-upload-hint">{text.chooseFileHint}</p>
        </Upload.Dragger>

        {m.preview.isPending && <Spin />}
        {m.preview.error && <Alert type="error" showIcon message={m.preview.error.message} />}
        {m.data && m.file && <SpecImportPreviewTabs data={m.data} />}
      </Space>
    </Modal>
  );
}
