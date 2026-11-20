import { fireEvent, render, screen } from '@testing-library/react';
import type { SpecImportPreview } from '../types';
import { SpecImportPreviewTabs } from './SpecImportPreviewTabs';

const preview = (over: Partial<SpecImportPreview> = {}): SpecImportPreview => ({
  productId: 1,
  productCode: 'AO01',
  orderId: null,
  orderCode: null,
  components: [],
  norms: [
    {
      rowNumber: 4,
      action: 'Update',
      error: null,
      color: 'Xanh',
      componentName: 'Tay áo',
      materialCode: 'VAI03',
      materialName: 'Vải trắng',
      unit: 'm',
      materialColor: 'Trắng',
      specNote: null,
    },
    {
      rowNumber: 5,
      action: 'Error',
      error: 'Màu "Tím" chưa có trong đơn hàng nào của mã hàng này.',
      color: 'Tím',
      componentName: 'Nhãn',
      materialCode: 'NHAN01',
      materialName: null,
      unit: null,
      materialColor: null,
      specNote: null,
    },
  ],
  markerGroups: [],
  orderMarkers: [],
  hasComponents: false,
  hasNorms: true,
  hasMarkerGroups: false,
  hasOrderMarkers: false,
  validCount: 1,
  errorCount: 1,
  duplicateCount: 0,
  changeCount: 1,
  crossErrors: [],
  notes: [],
  canImport: true,
  removedCount: 0,
  removedNames: null,
  ...over,
});

describe('SpecImportPreviewTabs', () => {
  it('tóm tắt theo MSG24, chỉ có tab của bảng có trong file, lý do dòng lỗi hiện khi trỏ vào nhãn', async () => {
    render(<SpecImportPreviewTabs data={preview()} />);

    expect(
      screen.getByText('1 dòng hợp lệ, 1 dòng lỗi, 0 dòng trùng. Chỉ các dòng hợp lệ được nhập.'),
    ).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Định mức NVL (2)' })).toBeInTheDocument();
    expect(screen.queryByRole('tab', { name: /Thành phần/ })).not.toBeInTheDocument();
    expect(screen.getByText('Cập nhật')).toBeInTheDocument();
    expect(screen.queryByText(/chưa có trong đơn hàng/)).not.toBeInTheDocument();
    fireEvent.mouseEnter(screen.getByRole('img', { name: 'info-circle' }));
    expect(await screen.findByText(/chưa có trong đơn hàng/)).toBeInTheDocument();
  });

  it('hiện lỗi của bản đã gộp và ghi chú', () => {
    render(
      <SpecImportPreviewTabs
        data={preview({
          crossErrors: ['Nhóm bìa 2, màu Xanh: các chi tiết dùng vải khác nhau.'],
          notes: ['Cột size XL không có trong đơn nên bị bỏ qua.'],
          canImport: false,
        })}
      />,
    );

    expect(screen.getByText(/còn lỗi dưới đây/)).toBeInTheDocument();
    expect(screen.getByText(/các chi tiết dùng vải khác nhau/)).toBeInTheDocument();
    expect(screen.getByText(/Cột size XL/)).toBeInTheDocument();
  });

  it('bìa của đơn: mỗi size của đơn một cột, không hiện size khác', () => {
    render(
      <SpecImportPreviewTabs
        data={preview({
          hasNorms: false,
          norms: [],
          hasOrderMarkers: true,
          orderSizes: ['S', 'M'],
          orderMarkers: [
            {
              rowNumber: 4,
              action: 'New',
              error: null,
              markerNo: 1,
              markerCode: '1A',
              lengthM: 2.2,
              widthM: 1.5,
              status: 'Active',
              ratios: [
                { sizeCode: 'S', piecesPerPly: 1 },
                { sizeCode: 'M', piecesPerPly: 2 },
              ],
            },
          ],
        })}
      />,
    );

    expect(screen.getByRole('columnheader', { name: 'S' })).toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: 'M' })).toBeInTheDocument();
    expect(screen.queryByRole('columnheader', { name: 'XL' })).not.toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: 'Trạng thái' })).toBeInTheDocument();
  });
});
