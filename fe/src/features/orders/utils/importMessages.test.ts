import { orderMaterialImportDoneMessage, specImportDoneMessage } from './importMessages';

const result = { components: 0, norms: 0, markerGroups: 0, orderMarkers: 3, removed: 0 };

describe('specImportDoneMessage', () => {
  it('chỉ nêu bảng vừa nhập, không liệt kê bảng 0 dòng của mục khác', () => {
    expect(specImportDoneMessage(result, ['markers'])).toBe('Đã lưu: 3 bìa của đơn.');
  });

  it('nêu số dòng đã xóa và NVL mới thêm', () => {
    const msg = specImportDoneMessage({ ...result, norms: 5, orderMarkers: 0, removed: 2 }, ['norms'], 1);
    expect(msg).toBe('Đã lưu: 5 dòng định mức. Đã xóa 2 dòng cũ không có trong bảng. Đã thêm 1 NVL mới vào danh mục.');
  });

  it('không có thay đổi thì báo rõ thay vì "0 …"', () => {
    expect(specImportDoneMessage({ ...result, orderMarkers: 0 }, ['markers'])).toBe(
      'Không có thay đổi nào so với dữ liệu đang có.',
    );
  });
});

describe('orderMaterialImportDoneMessage', () => {
  it('nêu thêm / cập nhật / xóa', () => {
    expect(orderMaterialImportDoneMessage({ created: 2, updated: 1, removed: 3 })).toBe(
      'Đã lưu NVL của đơn: thêm 2, cập nhật 1. Đã xóa 3 dòng cũ không có trong bảng.',
    );
  });

  it('không đổi gì', () => {
    expect(orderMaterialImportDoneMessage({ created: 0, updated: 0 })).toBe(
      'Không có thay đổi nào so với dữ liệu đang có.',
    );
  });
});
