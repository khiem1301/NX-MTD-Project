import { useState } from 'react';
import { Select } from 'antd';
import { useMaterialOptions } from '@/hooks/useLookups';
import { LABELS } from '../constants';
import { isNewMaterial, NEW_MATERIAL_PREFIX, newMaterialName } from '../newMaterial';

const MAX_NAME_LENGTH = 200;

type MaterialFreeSelectProps = {
  /** number = NVL có sẵn (materialId); "new:<tên>" = NVL mới gõ tay. */
  value?: number | string;
  onChange?: (value: number | string | undefined) => void;
  disabled?: boolean;
};

/** Chọn NVL trong danh mục, hoặc gõ tên NVL chưa có để thêm mới (VD: kim may). */
export function MaterialFreeSelect({ value, onChange, disabled }: MaterialFreeSelectProps) {
  const materials = useMaterialOptions();
  const [search, setSearch] = useState('');

  const options: { value: number | string; label: string }[] = (materials.data ?? []).map((m) => ({
    value: m.materialId,
    label: `${m.materialCode} ${m.materialName}${m.color ? ` - ${m.color}` : ''} (${m.unit})`,
  }));
  const typed = search.trim();
  // Cùng tên khác màu là NVL khác nên vẫn cho thêm mới; màu nhập ở dòng đơn mua.
  const exists = (materials.data ?? []).some(
    (m) => m.color === '' && m.materialName.toLowerCase() === typed.toLowerCase(),
  );
  if (typed && !exists) {
    options.push({
      value: `${NEW_MATERIAL_PREFIX}${typed}`,
      label: `${LABELS.addNewMaterial}: "${typed}"`,
    });
  }
  // Giữ nhãn của NVL mới đã chọn sau khi ô tìm kiếm bị xóa.
  if (isNewMaterial(value) && !options.some((o) => o.value === value)) {
    options.push({ value, label: `${LABELS.addNewMaterial}: "${newMaterialName(value)}"` });
  }

  return (
    <Select
      showSearch
      value={value}
      disabled={disabled}
      onChange={onChange}
      onSearch={(text) => setSearch(text.slice(0, MAX_NAME_LENGTH))}
      onBlur={() => setSearch('')}
      placeholder={LABELS.newMaterialHint}
      options={options}
      optionFilterProp="label"
      loading={materials.isLoading}
      style={{ width: 340 }}
    />
  );
}
