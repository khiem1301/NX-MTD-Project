import { render, screen } from '@testing-library/react';
import { NotificationTypeTags } from './NotificationTypeTags';

describe('NotificationTypeTags', () => {
  it('mỗi loại thông báo một nhãn màu riêng', () => {
    const { container, rerender } = render(
      <NotificationTypeTags n={{ sourceType: 'REQUEST', isUrgent: false }} />,
    );
    expect(screen.getByText('Đơn từ')).toBeInTheDocument();
    expect(container.querySelector('.ant-tag-purple')).not.toBeNull();

    rerender(<NotificationTypeTags n={{ sourceType: 'WAREHOUSE', isUrgent: false }} />);
    expect(container.querySelector('.ant-tag-orange')).not.toBeNull();
  });

  it('thông báo khẩn có thêm nhãn Khẩn màu đỏ', () => {
    const { container } = render(
      <NotificationTypeTags n={{ sourceType: 'ANNOUNCEMENT', isUrgent: true }} />,
    );
    expect(screen.getByText('Từ quản lý')).toBeInTheDocument();
    expect(screen.getByText('Khẩn')).toBeInTheDocument();
    expect(container.querySelector('.ant-tag-red')).not.toBeNull();
  });
});
