import { useState } from 'react';
import { message, Typography } from 'antd';
import { DownloadOutlined } from '@ant-design/icons';
import { API } from '@/constants/apiEndpoints';
import { MESSAGES } from '@/constants/messages';

export type ImportTemplateKind =
  'spec' | 'order-lines' | 'order-materials' | 'order-markers' | 'operations' | 'product-form';

type ImportTemplateLinkProps = {
  kind: ImportTemplateKind;
  /** Mã hàng đang mở: bảng của mã hàng đã có dữ liệu thì file mẫu chứa dữ liệu đó. */
  productId?: number | null;
  /** Đơn hàng đang mở: tương tự với bảng của đơn (bìa, dòng hàng, NVL). */
  orderId?: number | null;
  /** Chữ của nút khi một màn có nhiều file mẫu (mặc định "Tải file mẫu"). */
  label?: string;
};

const FILE_NAME: Record<ImportTemplateKind, string> = {
  spec: 'Mau_Dinh_Muc_Bia_Cat.xlsx',
  'order-lines': 'Mau_Dong_Hang_Don.xlsx',
  'order-materials': 'Mau_NVL_Cua_Don.xlsx',
  'order-markers': 'Mau_Bia_Cua_Don.xlsx',
  operations: 'Mau_Cong_Doan.xlsx',
  'product-form': 'Mau_Form_Ma_Hang.xlsx',
};

/**
 * Nút "Tải file mẫu" của từng chức năng nhập Excel. File do server dựng: bảng nào đã có dữ liệu (của mã hàng / đơn hàng đang mở)
 * thì ghi dữ liệu đó, bảng chưa có thì ghi dòng ví dụ chữ xám nghiêng.
 */
export function ImportTemplateLink({ kind, productId, orderId, label }: ImportTemplateLinkProps) {
  const [busy, setBusy] = useState(false);

  const download = async () => {
    setBusy(true);
    try {
      // nạp lúc bấm để màn hình không kéo theo apiClient khi chỉ hiển thị nút
      const { downloadBlob } = await import('@/utils/downloadFile');
      await downloadBlob(API.importTemplate(kind), FILE_NAME[kind], {
        productId: productId ?? undefined,
        orderId: orderId ?? undefined,
      });
    } catch {
      void message.error(MESSAGES.monthlyCost.templateDownloadFailed);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Typography.Link disabled={busy} onClick={() => void download()}>
      <DownloadOutlined /> {label ?? MESSAGES.orders.specImport.template}
    </Typography.Link>
  );
}
