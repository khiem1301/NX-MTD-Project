export const MESSAGES = {
  common: {
    networkError: 'Mất kết nối. Dữ liệu đang nhập vẫn được giữ, vui lòng thử lại.', // MSG28
    unknownError: 'Có lỗi xảy ra. Vui lòng thử lại.',
    loading: 'Đang tải...',
  },
  auth: {
    title: 'NX-MTD',
    subtitle: 'Đăng nhập hệ thống quản lý gia công may mặc',
    username: 'Tên đăng nhập',
    password: 'Mật khẩu',
    usernameRequired: 'Vui lòng nhập tên đăng nhập.', // MSG01
    passwordRequired: 'Vui lòng nhập mật khẩu.', // MSG01
    loginButton: 'Đăng nhập',
    logoutButton: 'Đăng xuất',
  },
  home: { greeting: 'Xin chào' },
} as const;
