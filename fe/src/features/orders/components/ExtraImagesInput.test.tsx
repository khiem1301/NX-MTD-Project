import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { useState } from 'react';
import { ExtraImagesInput } from './ExtraImagesInput';

const upload = jest.fn();

// Mock cả api và ảnh: cả hai kéo theo cấu hình môi trường (import.meta) mà jest không đọc được.
jest.mock('../api/orderApi', () => ({
  productApi: { uploadSampleImage: (file: File) => upload(file) },
}));
jest.mock('./ProductImage', () => ({
  ProductImage: ({ value }: { value: string }) => <span data-testid="thumb">{value}</span>,
}));

const keys = (n: number) => Array.from({ length: n }, (_, i) => `PRODUCT/2026/11/${i}.png`);
const png = (name = 'a.png') => new File(['x'], name, { type: 'image/png' });

const setup = (initial: string[]) => {
  const changes: string[][] = [];
  function Host() {
    const [value, setValue] = useState(initial);
    return (
      <ExtraImagesInput
        value={value}
        onChange={(next) => {
          changes.push(next);
          setValue(next);
        }}
      />
    );
  }
  const { container } = render(<Host />);
  const pick = (files: File[]) =>
    fireEvent.change(container.querySelector('input[type=file]') as HTMLInputElement, {
      target: { files },
    });
  return { changes, pick };
};

describe('ExtraImagesInput', () => {
  beforeEach(() => {
    upload.mockReset();
    let n = 0;
    upload.mockImplementation(() => Promise.resolve(`PRODUCT/2026/11/new${++n}.png`));
  });

  it('hiện các ảnh đã có và đếm số ảnh', () => {
    setup(keys(2));

    expect(screen.getAllByTestId('thumb')).toHaveLength(2);
    expect(screen.getByText(/Đã chọn 2\/5 ảnh phụ/)).toBeInTheDocument();
  });

  it('ảnh theo màu: đủ số ảnh tối đa truyền vào (3) thì không còn nút thêm', () => {
    render(<ExtraImagesInput value={keys(3)} max={3} countText="{0}/{1} ảnh" />);

    expect(screen.getByText('3/3 ảnh')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Thêm ảnh/ })).not.toBeInTheDocument();
  });

  it('bỏ một ảnh thì chỉ ảnh đó bị bỏ', () => {
    const { changes } = setup(keys(3));

    fireEvent.click(screen.getAllByRole('button', { name: 'Bỏ ảnh' })[1] as HTMLElement);

    expect(changes.at(-1)).toEqual([keys(3)[0], keys(3)[2]]);
  });

  it('đủ 5 ảnh thì không còn nút thêm', () => {
    setup(keys(5));

    expect(screen.queryByRole('button', { name: /Thêm ảnh/ })).not.toBeInTheDocument();
  });

  it('chọn ảnh thì tải lên rồi nối khóa mới vào cuối danh sách', async () => {
    const { changes, pick } = setup(keys(1));

    pick([png()]);

    await waitFor(() => expect(changes.at(-1)).toEqual([keys(1)[0], 'PRODUCT/2026/11/new1.png']));
  });

  it('chọn nhiều ảnh vượt số chỗ còn lại thì chỉ tải số ảnh còn chỗ', async () => {
    const { changes, pick } = setup(keys(4));

    pick([png('a.png'), png('b.png'), png('c.png')]);

    await waitFor(() => expect(changes.at(-1)).toHaveLength(5));
    expect(upload).toHaveBeenCalledTimes(1);
  });

  it('chọn nhiều ảnh còn đủ chỗ thì giữ đủ cả các ảnh dù tải xong chồng lên nhau', async () => {
    const { changes, pick } = setup([]);

    pick([png('a.png'), png('b.png'), png('c.png')]);

    await waitFor(() => expect(changes.at(-1)).toHaveLength(3));
    expect(new Set(changes.at(-1)).size).toBe(3);
  });

  it('tệp không phải ảnh thì không tải lên', async () => {
    const { changes, pick } = setup([]);

    pick([new File(['x'], 'a.pdf', { type: 'application/pdf' })]);

    await new Promise((r) => setTimeout(r, 50));
    expect(upload).not.toHaveBeenCalled();
    expect(changes).toHaveLength(0);
  });
});
