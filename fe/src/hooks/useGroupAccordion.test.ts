import { act, renderHook } from '@testing-library/react';
import { useGroupAccordion } from './useGroupAccordion';

type Row = { code: string };
const GROUPS = [
  { key: 'a', rows: [{ code: 'OP01' }, { code: 'OP02' }] },
  { key: 'b', rows: [{ code: 'OP03' }] },
];
const matches = (row: Row, keyword: string) => row.code.toLowerCase().includes(keyword);
const openKeys = (sections: { group: { key: string }; isOpen: boolean }[]) =>
  sections.filter((s) => s.isOpen).map((s) => s.group.key);

describe('useGroupAccordion', () => {
  beforeAll(() => {
    // jsdom không có CSS.escape, dùng ở bước cuộn tới nhóm vừa mở.
    globalThis.CSS = { escape: (v: string) => v } as unknown as typeof CSS;
  });

  it('mặc định mở nhóm đầu', () => {
    const { result } = renderHook(() => useGroupAccordion(GROUPS, matches));

    expect(openKeys(result.current.sections)).toEqual(['a']);
  });

  it('mở nhóm khác thì nhóm đang mở tự đóng, bấm lại thì đóng hết', () => {
    const { result } = renderHook(() => useGroupAccordion(GROUPS, matches));

    act(() => result.current.toggle('b'));
    expect(openKeys(result.current.sections)).toEqual(['b']);

    act(() => result.current.toggle('b'));
    expect(openKeys(result.current.sections)).toEqual([]);
  });

  it('tìm thì ẩn nhóm không khớp, mở nhóm khớp đầu và giữ đủ dòng của nhóm để điền cả nhóm', () => {
    const { result } = renderHook(() => useGroupAccordion(GROUPS, matches));

    act(() => result.current.setKeyword('op03'));

    expect(result.current.sections).toHaveLength(1);
    expect(openKeys(result.current.sections)).toEqual(['b']);
    expect(result.current.sections[0]?.shownRows).toEqual([{ code: 'OP03' }]);
  });
});
