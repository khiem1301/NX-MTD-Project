import { useEffect, useMemo, useRef, useState } from 'react';
import { SearchOutlined } from '@ant-design/icons';
import { AutoComplete, Input, type InputRef } from 'antd';
import type { RefSelectProps } from 'antd/es/select';
import { useNavigate } from 'react-router-dom';
import { normalizeText, type NavLeaf } from './menuModel';

type Option = { value: string; label: string; search: string };

/** Ô tìm nhanh chức năng: gõ không dấu cũng ra, Enter để vào, Ctrl+K để nhảy tới ô. */
export function GlobalSearch({ leaves }: { leaves: NavLeaf[] }) {
  const navigate = useNavigate();
  const ref = useRef<RefSelectProps>(null);
  const inputRef = useRef<InputRef>(null);
  const [text, setText] = useState('');

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault();
        ref.current?.focus();
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);

  const options = useMemo(() => {
    const groups = new Map<string, Option[]>();
    for (const leaf of leaves) {
      const list = groups.get(leaf.groupLabel) ?? [];
      list.push({ value: leaf.path, label: leaf.label, search: `${leaf.label} ${leaf.groupLabel}` });
      groups.set(leaf.groupLabel, list);
    }
    return [...groups.entries()].map(([label, items]) => ({ label, options: items }));
  }, [leaves]);

  return (
    <AutoComplete
      ref={ref}
      className="app-search"
      value={text}
      options={options}
      onChange={setText}
      onSelect={(path: string) => {
        setText('');
        inputRef.current?.blur();
        navigate(path);
      }}
      filterOption={(input, option) =>
        normalizeText((option as Option | undefined)?.search ?? '').includes(normalizeText(input.trim()))
      }
      popupMatchSelectWidth={320}
      notFoundContent="Không tìm thấy chức năng"
    >
      <Input
        ref={inputRef}
        allowClear
        prefix={<SearchOutlined style={{ color: '#8c8c8c' }} />}
        suffix={text ? null : <span className="app-kbd">Ctrl K</span>}
        placeholder="Tìm chức năng…"
        variant="filled"
        aria-label="Tìm chức năng"
      />
    </AutoComplete>
  );
}
