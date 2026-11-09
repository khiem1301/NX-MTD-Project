import { fireEvent, render, screen } from '@testing-library/react';
import { useState } from 'react';
import { DataTable } from '@/components/common/DataTable';
import { toMaterialOption, type NormRow } from '../utils/productSpec';
import { specNormColumns } from './specNormColumns';

const FULL_LABEL = 'V-01 - Vải thun cotton 2 chiều - Đen (m)';
const material = toMaterialOption({
  materialId: 1,
  materialCode: 'V-01',
  materialName: 'Vải thun cotton 2 chiều',
  color: 'Đen',
  unit: 'm',
});

const row: NormRow = {
  key: 'n1',
  componentKey: 'c1',
  materialId: 1,
  specNote: '',
  variantKeys: [],
};

const mockAddMaterial = jest.fn();

function Host() {
  const [norms, setNorms] = useState([row]);
  return (
    <DataTable<NormRow>
      rowKey="key"
      pagination={false}
      dataSource={norms}
      columns={specNormColumns({
        componentOptions: [{ value: 'c1', label: 'Thân' }],
        materialOptions: [material],
        materialsLoading: false,
        variantOptions: [],
        setNorms,
        onAddMaterial: mockAddMaterial,
      })}
    />
  );
}

describe('cột NVL của bảng định mức', () => {
  it('ô đã chọn chỉ hiện mã - màu, không hiện tên và đơn vị', () => {
    render(<Host />);

    expect(screen.getByText('V-01 - Đen')).toBeInTheDocument();
    expect(screen.queryByText(/Vải thun/)).not.toBeInTheDocument();
  });

  it('trỏ chuột vào ô thì hiện đủ mã, tên, màu và đơn vị', async () => {
    const { container } = render(<Host />);

    // Ô chọn đầu tiên của dòng là Thành phần; ô thứ hai mới là NVL.
    const materialSelect = container.querySelectorAll('.ant-select')[1] as HTMLElement;
    fireEvent.mouseEnter(materialSelect);

    expect(await screen.findByText(FULL_LABEL)).toBeInTheDocument();
  });

  it('danh sách chọn NVL có nút Thêm NVL mới cho đúng dòng', () => {
    const { container } = render(<Host />);

    const materialSelect = container.querySelectorAll('.ant-select-selector')[1] as HTMLElement;
    fireEvent.mouseDown(materialSelect);
    fireEvent.click(screen.getByRole('button', { name: /Thêm NVL mới/ }));

    expect(mockAddMaterial).toHaveBeenCalledWith('n1');
  });

  it('nhãn đầy đủ vẫn dùng để tìm kiếm trong danh sách chọn', () => {
    expect(material).toMatchObject({ value: 1, label: FULL_LABEL, short: 'V-01 - Đen' });
  });

  it('NVL không phân màu thì chỉ hiện mã', () => {
    expect(
      toMaterialOption({
        materialId: 2,
        materialCode: 'K-01',
        materialName: 'Khóa kéo',
        color: '',
        unit: 'cái',
      }),
    ).toMatchObject({ label: 'K-01 - Khóa kéo (cái)', short: 'K-01' });
  });
});
