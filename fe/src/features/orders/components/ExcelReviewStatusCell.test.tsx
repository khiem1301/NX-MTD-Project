import { fireEvent, render, screen } from '@testing-library/react';
import { ExcelReviewStatusCell } from './ExcelReviewStatusCell';

const discontinued = { action: 'Error', error: 'Mã NVL "VAI-CU" đã ngừng sử dụng.' };

describe('ExcelReviewStatusCell — mở lại NVL ngừng sử dụng', () => {
  it('dòng lỗi vì NVL ngừng sử dụng có nút Mở lại NVL và gọi đúng thao tác', () => {
    const onReopen = jest.fn();
    render(<ExcelReviewStatusCell status={discontinued} onReopen={onReopen} />);

    fireEvent.click(screen.getByRole('button', { name: /Mở lại NVL/ }));

    expect(onReopen).toHaveBeenCalledTimes(1);
  });

  it('không có thao tác mở lại (không phải Admin) thì không hiện nút', () => {
    render(<ExcelReviewStatusCell status={discontinued} />);

    expect(screen.queryByRole('button', { name: /Mở lại NVL/ })).toBeNull();
  });

  it('lỗi khác không hiện nút Mở lại NVL', () => {
    render(
      <ExcelReviewStatusCell
        status={{ action: 'Error', error: 'Số cần không được âm.' }}
        onReopen={jest.fn()}
      />,
    );

    expect(screen.queryByRole('button', { name: /Mở lại NVL/ })).toBeNull();
  });
});
