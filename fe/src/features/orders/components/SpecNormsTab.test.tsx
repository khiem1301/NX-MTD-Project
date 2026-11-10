import { useState } from 'react';
import { fireEvent, render, screen } from '@testing-library/react';
import type { NormRow } from '../utils/productSpec';
import { SpecNormsTab } from './SpecNormsTab';

jest.mock('@/features/warehouse', () => ({
  MaterialFormModal: ({
    onSaved,
    onClose,
  }: {
    onSaved: (m: { materialId: number }) => void;
    onClose: () => void;
  }) => (
    <button
      onClick={() => {
        onSaved({ materialId: 9 });
        onClose();
      }}
    >
      lưu NVL mới
    </button>
  ),
}));

const row = (key: string): NormRow => ({
  key,
  componentKey: 'c1',
  materialId: 1,
  specNote: '',
  colorKeys: [],
});

function Host() {
  const [norms, setNorms] = useState([row('n1'), row('n2')]);
  return (
    <>
      <SpecNormsTab
        norms={norms}
        setNorms={setNorms}
        variants={[]}
        components={[]}
        materialOptions={[{ value: 1, label: 'V-01', short: 'V-01', isFabric: true }]}
        materialsLoading={false}
      />
      <output>{norms.map((n) => n.materialId).join(',')}</output>
    </>
  );
}

describe('SpecNormsTab', () => {
  it('tạo NVL mới từ ô chọn thì chọn NVL đó cho đúng dòng và đóng hộp', async () => {
    const { container } = render(<Host />);

    // Mỗi dòng có ba ô chọn: Thành phần, NVL, Áp dụng; ô NVL của dòng 2 đứng thứ năm.
    const selects = container.querySelectorAll('.ant-table-tbody .ant-select-selector');
    fireEvent.mouseDown(selects[4] as HTMLElement);
    fireEvent.click(screen.getByRole('button', { name: /Thêm NVL mới/ }));
    fireEvent.click(await screen.findByRole('button', { name: 'lưu NVL mới' }));

    expect(screen.getByRole('status')).toHaveTextContent('1,9');
    expect(screen.queryByRole('button', { name: 'lưu NVL mới' })).not.toBeInTheDocument();
  });
});
