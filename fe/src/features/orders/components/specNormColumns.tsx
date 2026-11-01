import type { Dispatch, SetStateAction } from 'react';
import { Button, Input, InputNumber, Select, Tooltip, type TableColumnsType } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { dropRow, patchRow, type MaterialOption, type NormRow } from '../utils/productSpec';
import { deleteRowColumn } from './deleteRowColumn';

const text = MESSAGES.orders.spec;

type SpecNormColumnsOptions = {
  componentOptions: { value: string; label: string }[];
  materialOptions: MaterialOption[];
  materialsLoading: boolean;
  variantOptions: { value: string; label: string }[];
  setNorms: Dispatch<SetStateAction<NormRow[]>>;
};

/** Cột bảng "Định mức NVL": ô nhập trực tiếp trên từng dòng, cột đầu và cột xóa cố định khi cuộn ngang. */
export const specNormColumns = ({
  componentOptions,
  materialOptions,
  materialsLoading,
  variantOptions,
  setNorms,
}: SpecNormColumnsOptions): TableColumnsType<NormRow> => [
  {
    title: text.component,
    fixed: 'left' as const,
    render: (_, r) => (
      <Select
        size="small"
        value={r.componentKey}
        options={componentOptions}
        style={{ width: 150 }}
        onChange={(v) => patchRow(setNorms, r.key, { componentKey: v })}
      />
    ),
  },
  {
    title: text.material,
    // Ô đã chọn chỉ hiện "mã - màu" cho cột gọn; trỏ chuột mới hiện đủ tên, danh sách chọn vẫn hiện và tìm theo đủ.
    render: (_, r) => (
      <Tooltip title={materialOptions.find((m) => m.value === r.materialId)?.label}>
        <Select
          size="small"
          showSearch
          optionFilterProp="label"
          optionLabelProp="short"
          value={r.materialId}
          options={materialOptions}
          loading={materialsLoading}
          popupMatchSelectWidth={false}
          style={{ width: 200 }}
          onChange={(v) => patchRow(setNorms, r.key, { materialId: v })}
        />
      </Tooltip>
    ),
  },
  {
    title: text.length,
    render: (_, r) => (
      <InputNumber
        size="small"
        min={0}
        precision={2}
        value={r.lengthCm}
        style={{ width: 90 }}
        onChange={(v) => patchRow(setNorms, r.key, { lengthCm: v })}
      />
    ),
  },
  {
    title: text.width,
    render: (_, r) => (
      <InputNumber
        size="small"
        min={0}
        precision={2}
        value={r.widthCm}
        style={{ width: 90 }}
        onChange={(v) => patchRow(setNorms, r.key, { widthCm: v })}
      />
    ),
  },
  {
    title: text.appliesTo,
    render: (_, r) => (
      <Select
        size="small"
        mode="multiple"
        maxTagCount="responsive"
        value={r.variantKeys}
        options={variantOptions}
        style={{ minWidth: 220 }}
        onChange={(v) => patchRow(setNorms, r.key, { variantKeys: v })}
        dropdownRender={(menu) => (
          <>
            {menu}
            <Button
              type="link"
              size="small"
              onClick={() =>
                patchRow(setNorms, r.key, {
                  variantKeys: variantOptions.map((o) => o.value),
                })
              }
            >
              {text.allVariants}
            </Button>
          </>
        )}
      />
    ),
  },
  {
    title: text.note,
    render: (_, r) => (
      <Input
        size="small"
        value={r.specNote}
        maxLength={200}
        style={{ width: 160 }}
        onChange={(e) => patchRow(setNorms, r.key, { specNote: e.target.value })}
      />
    ),
  },
  deleteRowColumn<NormRow>((key) => dropRow(setNorms, key)),
];
