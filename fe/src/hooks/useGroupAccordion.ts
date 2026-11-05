import { useState } from 'react';
import type { CSSProperties } from 'react';
import { foldVietnamese } from '@/utils/colorSearch';

type AccordionGroup<T> = { key: string; rows: readonly T[] };

export type AccordionSection<G extends AccordionGroup<T>, T> = {
  group: G;
  /** Dòng của nhóm khớp ô tìm (không tìm thì là cả nhóm). Ô "Cả nhóm" vẫn điền cho `group.rows`. */
  shownRows: readonly T[];
  isOpen: boolean;
};

/** Màu nền đặc (không trong suốt) để dòng nhóm đang mở dính đầu bảng mà không lộ dòng cuộn bên dưới. */
const GROUP_ROW_BACKGROUND = '#f7f7f7';
const GROUP_ROW_STYLE: CSSProperties = { background: GROUP_ROW_BACKGROUND, cursor: 'pointer' };
const OPEN_GROUP_ROW_STYLE: CSSProperties = {
  ...GROUP_ROW_STYLE,
  position: 'sticky',
  top: 0,
  zIndex: 3,
};

/** Key dòng nhóm trong bảng, dùng chung để cuộn tới dòng nhóm vừa mở. */
export const groupRowKey = (groupKey: string) => `g:${groupKey}`;

const scrollToGroupRow = (groupKey: string) =>
  requestAnimationFrame(() =>
    document
      .querySelector(`[data-row-key="${CSS.escape(groupRowKey(groupKey))}"]`)
      ?.scrollIntoView({ block: 'nearest' }),
  );

/**
 * Bảng công đoạn chia theo nhóm, mỗi lúc chỉ mở một nhóm: bấm nhóm khác thì nhóm đang mở tự đóng, bấm lại nhóm đang mở
 * thì đóng. Mặc định mở nhóm đầu. Ô tìm lọc công đoạn trong mọi nhóm, ẩn nhóm không có công đoạn khớp và mở nhóm khớp đầu
 * tiên. Dòng nhóm đang mở dính đầu bảng khi cuộn qua nhóm nhiều công đoạn.
 */
export const useGroupAccordion = <T, G extends AccordionGroup<T>>(
  groups: readonly G[],
  matches: (row: T, keyword: string) => boolean,
) => {
  // undefined = chưa chọn (mở nhóm đầu); null = đã đóng hết.
  const [openKey, setOpenKey] = useState<string | null | undefined>(undefined);
  const [keyword, setKeywordState] = useState('');

  const normalized = foldVietnamese(keyword);
  const visible = groups
    .map((group) => ({
      group,
      shownRows: normalized ? group.rows.filter((r) => matches(r, normalized)) : group.rows,
    }))
    .filter((s) => !normalized || s.shownRows.length > 0);
  const currentKey =
    openKey === undefined || (openKey !== null && !visible.some((s) => s.group.key === openKey))
      ? (visible[0]?.group.key ?? null)
      : openKey;

  const sections: AccordionSection<G, T>[] = visible.map((s) => ({
    ...s,
    isOpen: s.group.key === currentKey,
  }));

  const toggle = (groupKey: string) => {
    const next = currentKey === groupKey ? null : groupKey;
    setOpenKey(next);
    if (next !== null) scrollToGroupRow(next);
  };

  const setKeyword = (value: string) => {
    setKeywordState(value);
    setOpenKey(undefined);
  };

  /** Thuộc tính dòng nhóm cho `onRow` của bảng. */
  const groupRowProps = (groupKey: string, isOpen: boolean) => ({
    style: isOpen ? OPEN_GROUP_ROW_STYLE : GROUP_ROW_STYLE,
    onClick: () => toggle(groupKey),
  });

  return { sections, keyword, setKeyword, toggle, groupRowProps };
};
