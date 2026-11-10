import { render, screen } from '@testing-library/react';
import { TempPasswordModal } from './TempPasswordModal';

describe('TempPasswordModal', () => {
  it('hiện tài khoản + mật khẩu tạm; đóng rồi render lại không còn mật khẩu trong DOM', () => {
    const { rerender } = render(
      <TempPasswordModal
        value={{ username: 'CNMAY2', temporaryPassword: '482913' }}
        onClose={jest.fn()}
      />,
    );
    expect(screen.getByText('482913')).toBeInTheDocument();
    expect(screen.getByText('CNMAY2')).toBeInTheDocument();

    rerender(<TempPasswordModal value={null} onClose={jest.fn()} />);
    expect(document.body.textContent).not.toContain('482913');
  });
});
