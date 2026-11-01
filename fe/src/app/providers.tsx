import { type ReactNode } from 'react';
import { App as AntApp, ConfigProvider, Empty } from 'antd';
import { MESSAGES } from '@/constants/messages';
// Bản ES: bản 'antd/locale' là CommonJS, Vite bọc thành { default } nên antd không nhận được tiếng Việt.
import viVN from 'antd/es/locale/vi_VN';
import dayjs from 'dayjs';
import 'dayjs/locale/vi';
import { QueryClientProvider } from '@tanstack/react-query';
import { antdTheme } from '@/app/theme';
import { queryClient } from '@/services/queryClient';
import { AuthBootstrap } from './AuthBootstrap';

// Tên tháng, thứ trong ô chọn ngày lấy từ dayjs: không đặt locale thì hiện tiếng Anh (Jan, Feb...). Tuần bắt đầu từ thứ Hai.
dayjs.locale('vi');

/** Nhãn nút chung cho Modal và hộp xác nhận: Lưu / Hủy cho form, Xác nhận / Hủy cho Popconfirm. */
const APP_LOCALE = {
  ...viVN,
  Modal: {
    justOkText: viVN.Modal?.justOkText ?? 'Đồng ý',
    okText: MESSAGES.common.save,
    cancelText: MESSAGES.common.cancel,
  },
  Popconfirm: {
    ...viVN.Popconfirm,
    okText: MESSAGES.common.confirm,
    cancelText: MESSAGES.common.cancel,
  },
};

export function Providers({ children }: { children: ReactNode }) {
  return (
    <ConfigProvider
      locale={APP_LOCALE}
      theme={antdTheme}
      renderEmpty={() => (
        <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={MESSAGES.common.noData} />
      )}
    >
      <AntApp>
        <QueryClientProvider client={queryClient}>
          <AuthBootstrap>{children}</AuthBootstrap>
        </QueryClientProvider>
      </AntApp>
    </ConfigProvider>
  );
}
