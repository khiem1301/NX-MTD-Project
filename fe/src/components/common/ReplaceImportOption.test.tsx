import { fireEvent, render, screen } from '@testing-library/react';
import { ReplaceImportOption } from './ReplaceImportOption';

describe('ReplaceImportOption', () => {
  it('chưa bật thì không hiện cảnh báo dòng sẽ bị bỏ, dù máy chủ đã trả danh sách', () => {
    render(
      <ReplaceImportOption checked={false} onChange={jest.fn()} removed={['Thành phần: Cổ áo']} />,
    );

    expect(screen.queryByText(/Sẽ xóa/)).toBeNull();
  });

  it('bật thì liệt kê dòng sẽ bị xóa và dòng không xóa được', () => {
    render(
      <ReplaceImportOption
        checked
        onChange={jest.fn()}
        unit="NVL"
        removed={['KHOA01', 'CHI01']}
        blocked={['VAI01']}
      />,
    );

    expect(screen.getByText('Sẽ xóa 2 NVL không có trong file')).toBeTruthy();
    expect(screen.getByText('KHOA01; CHI01')).toBeTruthy();
    expect(screen.getByText('Không xóa được 1 NVL (đang được dùng), giữ nguyên')).toBeTruthy();
  });

  it('công đoạn: ghi là ngừng dùng chứ không phải xóa', () => {
    render(
      <ReplaceImportOption checked onChange={jest.fn()} unit="công đoạn" discontinue removed={['OP09 Vắt sổ']} />,
    );

    expect(screen.getByText('Sẽ ngừng dùng 1 công đoạn không có trong file')).toBeTruthy();
  });

  it('rút gọn khi quá nhiều dòng', () => {
    const many = Array.from({ length: 11 }, (_, i) => `D${i + 1}`);
    render(<ReplaceImportOption checked onChange={jest.fn()} removed={many} />);

    expect(screen.getByText(/D1; D2.*D8 …và 3 dòng khác/)).toBeTruthy();
  });

  it('đổi trạng thái ô thì báo ra ngoài', () => {
    const onChange = jest.fn();
    render(<ReplaceImportOption checked={false} onChange={onChange} />);

    fireEvent.click(screen.getByRole('checkbox'));

    expect(onChange).toHaveBeenCalledWith(true);
  });
});
