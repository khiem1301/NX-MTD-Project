import type { Dispatch, SetStateAction } from 'react';
import { PlusOutlined } from '@ant-design/icons';
import { Button, Divider, Input, Select, Tooltip, type TableColumnsType } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { dropRow, patchRow, type MaterialOption, type NormRow } from '../utils/productSpec';
import { deleteRowColumn } from './deleteRowColumn';

const text = MESSAGES.orders.spec;

type SpecNormColumnsOptions = {
  componentOptions: { value: string; label: string }[];
  materialOptions: MaterialOption[];
  materialsLoading: boolean;
  /** Màu áo chọn được (định mức theo màu, DEV-ALL-87). */
  colorOptions: { value: string; label: string }[];
  setNorms: Dispatch<SetStateAction<NormRow[]>>;
  /** Tạo NVL mới ngay tại ô chọn NVL của dòng này. */
  onAddMaterial: (rowKey: string) => void;
};

/** Cột bảng "Định mức NVL": ô nhập trực tiếp trên từng dòng, cột đầu và cột xóa cố định khi cuộn ngang. */
export const specNormColumns = ({
  componentOptions,
  materialOptions,
  materialsLoading,
  colorOptions,
  setNorms,
  onAddMaterial,
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
          dropdownRender={(menu) => (
            <>
              {menu}
              <Divider style={{ margin: '8px 0' }} />
              <Button type="link" icon={<PlusOutlined />} onClick={() => onAddMaterial(r.key)}>
                {text.addMaterial}
              </Button>
            </>
          )}
        />
      </Tooltip>
    ),
  },
  {
    title: text.appliesTo,
    render: (_, r) => (
      <Select
        size="small"
        mode="multiple"
        maxTagCount="responsive"
        value={r.colorKeys}
        options={colorOptions}
        style={{ minWidth: 220 }}
        onChange={(v) => patchRow(setNorms, r.key, { colorKeys: v })}
        dropdownRender={(menu) => (
          <>
            {menu}
            <Button
              type="link"
              size="small"
              onClick={() =>
                patchRow(setNorms, r.key, {
                  colorKeys: colorOptions.map((o) => o.value),
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
