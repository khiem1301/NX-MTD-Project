import { InboxOutlined } from '@ant-design/icons';
import { Alert, Button, Modal, Space, Upload } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { DataTable } from '@/components/common/DataTable';
import { useEmployeeImport, type ImportCreated, type ImportRow } from '../hooks/useEmployeeImport';
import { createdColumns, failedColumns, previewColumns } from './employeeImportColumns';

const HEADERS =
  'Mã NV | Họ tên | Giới tính | Ngày sinh | SĐT | Vai trò | Mã tổ | Hình thức lương | Ngày vào làm | Mã chấm công';

/**
 * KH-22 (UC17 import): Admin chọn file Excel nhân viên → xem trước 3 loại hợp lệ / lỗi / trùng (MSG24) → nhập các dòng hợp lệ.
 * Mật khẩu tạm của người vừa nhập chỉ hiện một lần trong hộp này (D29): không lưu store, đóng hộp là mất.
 */
export function EmployeeImportModal({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { file, setFile, preview, run, data, result, copyAll } = useEmployeeImport();

  return (
    <Modal
      open={open}
      width={MODAL_WIDTH.lg}
      title="Nhập nhân viên từ Excel"
      onCancel={onClose}
      destroyOnHidden
      footer={
        result ? (
          <Button type="primary" onClick={onClose}>
            {MESSAGES.common.close}
          </Button>
        ) : (
          <Space>
            <Button onClick={onClose}>{MESSAGES.common.cancel}</Button>
            <Button
              type="primary"
              loading={run.isPending}
              disabled={!file || !data || data.validCount === 0}
              onClick={() =>
                file &&
                Modal.confirm({
                  title: 'Nhập các dòng hợp lệ?',
                  content: `${data?.validCount ?? 0} nhân viên sẽ được tạo kèm tài khoản và mật khẩu tạm.`,
                  okText: MESSAGES.common.confirm,
                  cancelText: MESSAGES.common.cancel,
                  onOk: () => run.mutateAsync(file),
                })
              }
            >
              Nhập {data?.validCount ?? 0} dòng hợp lệ
            </Button>
          </Space>
        )
      }
    >
      {!result && (
        <>
          <Alert
            type="info"
            showIcon
            style={{ marginBottom: 12 }}
            message={`Dòng đầu là tiêu đề, các cột theo thứ tự: ${HEADERS}.`}
            description="Vai trò: Worker, TeamLeader, TeamDeputy, QaQc, Electrician, WarehouseKeeper, Accountant, Manager, Admin. Hình thức lương: PieceRate / Daily / Monthly. Ngày: dd/MM/yyyy."
          />
          <Upload.Dragger
            accept=".xlsx"
            maxCount={1}
            beforeUpload={(f) => {
              setFile(f);
              preview.mutate(f);
              return false;
            }}
            onRemove={() => {
              setFile(null);
              preview.reset();
            }}
            style={{ marginBottom: 16 }}
          >
            <p className="ant-upload-drag-icon">
              <InboxOutlined />
            </p>
            <p>Chọn hoặc kéo thả file .xlsx</p>
          </Upload.Dragger>
          {data && (
            <>
              <Alert
                type={data.errorCount + data.duplicateCount > 0 ? 'warning' : 'success'}
                showIcon
                message={data.summary}
                style={{ marginBottom: 12 }}
              />
              <DataTable<ImportRow>
                rowKey="rowNumber"
                size="small"
                dataSource={data.rows}
                columns={previewColumns}
              />
            </>
          )}
        </>
      )}
      {result && (
        <>
          <Alert
            type={result.failedCount > 0 ? 'warning' : 'success'}
            showIcon
            style={{ marginBottom: 12 }}
            message={`Đã nhập ${result.createdCount} nhân viên, ${result.failedCount} dòng không nhập được.`}
            description="Mật khẩu tạm chỉ hiện một lần ở đây. Hãy sao chép và gửi cho từng người; đóng hộp này là không xem lại được."
            action={<Button onClick={() => void copyAll()}>Sao chép tất cả</Button>}
          />
          <DataTable<ImportCreated>
            rowKey="username"
            size="small"
            dataSource={result.created}
            columns={createdColumns}
          />
          {result.failed.length > 0 && (
            <DataTable<ImportRow>
              style={{ marginTop: 12 }}
              rowKey="rowNumber"
              size="small"
              dataSource={result.failed}
              pagination={false}
              columns={failedColumns}
            />
          )}
        </>
      )}
    </Modal>
  );
}
