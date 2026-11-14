import type { OrderMarkerProduct } from '../types';
import {
  markerVersion,
  newOrderMarkerRow,
  narrowFabricsOf,
  tooWideWarning,
  toOrderMarkerRows,
  toOrderMarkersPayload,
  validateOrderMarkers,
  type OrderMarkerRow,
} from './orderMarker';

const sizes = ['S', 'M', 'L'];
const product: OrderMarkerProduct = {
  productId: 7,
  productCode: 'AO-01',
  productName: 'Áo',
  sizes,
  markerGroups: [
    {
      markerNo: 1,
      colors: ['Đen', 'Xanh'],
      fabrics: [
        { materialId: 1, materialCode: 'V01', materialName: 'Vải', color: 'Đen', widthM: 1.5 },
        { materialId: 2, materialCode: 'V02', materialName: 'Vải', color: 'Xanh', widthM: 1.6 },
      ],
    },
  ],
  markers: [
    {
      productMarkerId: 3,
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
};

const row = (change: Partial<OrderMarkerRow> = {}): OrderMarkerRow => ({
  ...(toOrderMarkerRows(product)[0] as OrderMarkerRow),
  ...change,
});

describe('toOrderMarkerRows, toOrderMarkersPayload', () => {
  it('giữ id bìa, chỉ gửi size có số sản phẩm và viết hoa mã bìa', () => {
    const payload = toOrderMarkersPayload(
      [row({ markerCode: ' 1a ', pieces: { S: 1, M: 2, L: null } })],
      sizes,
    );

    expect(payload.markers).toEqual([
      {
        productMarkerId: 3,
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
    ]);
  });

  it('bìa mới tự chọn nhóm khi mã hàng chỉ có một nhóm', () => {
    expect(newOrderMarkerRow(product).markerNo).toBe(1);
    const twoGroups = {
      ...product,
      markerGroups: [
        { markerNo: 1, colors: ['Đen'], fabrics: [] },
        { markerNo: 2, colors: ['Xanh'], fabrics: [] },
      ],
    };
    expect(newOrderMarkerRow(twoGroups).markerNo).toBeUndefined();
  });
});

describe('validateOrderMarkers', () => {
  it('bìa đủ thông tin thì hợp lệ', () => {
    expect(validateOrderMarkers([row()], sizes)).toBe('');
  });

  it.each([
    ['chưa chọn nhóm', { markerNo: undefined }, 'nhóm bìa'],
    ['mã có khoảng trắng', { markerCode: '1 A' }, 'mã bìa'],
    ['mã quá 20 ký tự', { markerCode: 'x'.repeat(21) }, 'mã bìa'],
    ['dài bằng 0', { lengthM: 0 }, 'dài bìa'],
    ['dài vượt giới hạn', { lengthM: 100000 }, 'dài bìa'],
    ['chưa nhập khổ', { widthM: null }, 'khổ bìa'],
    ['khổ bằng 0', { widthM: 0 }, 'khổ bìa'],
    ['khổ vượt 3,5 m', { widthM: 3.51 }, 'khổ bìa'],
    ['số sản phẩm vượt 999', { pieces: { S: 1000 } }, 'số sản phẩm'],
    ['không có size nào', { pieces: {} }, 'ít nhất một size'],
  ])('%s thì báo lỗi', (_, change, expected) => {
    expect(validateOrderMarkers([row(change)], sizes)).toContain(expected);
  });

  it('khổ đúng 3,5 m vẫn hợp lệ', () => {
    expect(validateOrderMarkers([row({ widthM: 3.5 })], sizes)).toBe('');
  });

  it('dài 99.999,99 m và 999 sản phẩm vẫn hợp lệ', () => {
    expect(validateOrderMarkers([row({ lengthM: 99999.99, pieces: { M: 999 } })], sizes)).toBe('');
  });

  it('trùng mã bìa trong cùng nhóm thì báo, khác nhóm thì được', () => {
    expect(validateOrderMarkers([row(), row({ key: 'x', markerCode: '1a' })], sizes)).toContain(
      'trùng',
    );
    expect(validateOrderMarkers([row(), row({ key: 'x', markerNo: 2 })], sizes)).toBe('');
  });
});

describe('markerVersion', () => {
  it('tải lại cùng dữ liệu thì không đổi (bảng không dựng lại), bìa đổi sau khi lưu thì đổi', () => {
    const reloaded = JSON.parse(JSON.stringify(product)) as typeof product;
    const saved = {
      ...product,
      markers: [{ ...(product.markers[0] as (typeof product.markers)[number]), lengthM: 2.3 }],
    };

    expect(markerVersion(reloaded)).toBe(markerVersion(product));
    expect(markerVersion(saved)).not.toBe(markerVersion(product));
  });
});

describe('narrowFabricsOf, tooWideWarning (DEV-ALL-104)', () => {
  const groups = product.markerGroups;

  it('bìa rộng hơn khổ vải của nhóm thì liệt kê vải hẹp hơn', () => {
    expect(narrowFabricsOf(row({ widthM: 1.55 }), groups).map((f) => f.materialCode)).toEqual([
      'V01',
    ]);
    expect(tooWideWarning(row({ widthM: 1.7 }), groups)).toBe(
      'Rộng hơn khổ vải V01 (1,5 m), V02 (1,6 m): vải không đủ rộng để cắt bằng bìa này',
    );
  });

  it('bìa vừa khổ vải, chưa nhập khổ hoặc chưa chọn nhóm thì không cảnh báo', () => {
    expect(tooWideWarning(row({ widthM: 1.5 }), groups)).toBe('');
    expect(tooWideWarning(row({ widthM: null }), groups)).toBe('');
    expect(tooWideWarning(row({ markerNo: undefined, widthM: 3 }), groups)).toBe('');
  });

  it('vải chưa có khổ thì không so', () => {
    const noWidth = [
      {
        markerNo: 1,
        colors: ['Đen'],
        fabrics: [
          { materialId: 1, materialCode: 'V01', materialName: 'Vải', color: 'Đen', widthM: null },
        ],
      },
    ];
    expect(tooWideWarning(row({ widthM: 3 }), noWidth)).toBe('');
  });
});
