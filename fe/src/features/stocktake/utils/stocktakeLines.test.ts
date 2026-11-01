import { MAX_LINES, targetKey, toStocktakeLines, type LineTarget } from './stocktakeLines';

const material = (id: number): LineTarget => ({
  materialId: id,
  orderLineId: null,
  productComponentMaterialId: null,
});
const piece = (orderLineId: number, pcm: number | null = null): LineTarget => ({
  materialId: null,
  orderLineId,
  productComponentMaterialId: pcm,
});

describe('targetKey', () => {
  it('kho NVL theo NVL, kho BTP theo dòng đơn và chi tiết lẻ', () => {
    expect(targetKey(material(7))).toBe('7');
    expect(targetKey(piece(101))).toBe('101:');
    expect(targetKey(piece(101, 5))).toBe('101:5');
  });
});

describe('toStocktakeLines', () => {
  const targets = new Map([
    ['1', material(1)],
    ['2', material(2)],
    ['3', material(3)],
  ]);

  it('chỉ gửi dòng có số đếm hoặc ghi chú, kể cả số đếm 0', () => {
    const lines = toStocktakeLines(
      { counted: { '1': 0, '2': null }, notes: { '2': '  hỏng  ', '3': '   ' } },
      targets,
    );

    expect(lines).toEqual([
      {
        materialId: 1,
        orderLineId: undefined,
        productComponentMaterialId: null,
        countedQty: 0,
        note: undefined,
      },
      {
        materialId: 2,
        orderLineId: undefined,
        productComponentMaterialId: null,
        countedQty: null,
        note: 'hỏng',
      },
    ]);
  });

  it('giữ số đã nhập ở trang khác vì duyệt theo mọi dòng đã gặp', () => {
    const lines = toStocktakeLines({ counted: { '3': 9 } }, targets);

    expect(lines.map((l) => l.materialId)).toEqual([3]);
  });

  it('không có gì được nhập thì không có dòng nào', () => {
    expect(toStocktakeLines({}, targets)).toEqual([]);
  });

  it('dòng BTP gửi dòng đơn và chi tiết lẻ', () => {
    const lines = toStocktakeLines(
      { counted: { '101:5': 4 } },
      new Map([['101:5', piece(101, 5)]]),
    );

    expect(lines).toEqual([
      {
        materialId: undefined,
        orderLineId: 101,
        productComponentMaterialId: 5,
        countedQty: 4,
        note: undefined,
      },
    ]);
  });

  it('giới hạn dòng khớp BE', () => {
    expect(MAX_LINES).toBe(1000);
  });
});
