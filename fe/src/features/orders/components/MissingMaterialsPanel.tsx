import { Alert, AutoComplete, Input, InputNumber, Select, Table, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { MESSAGES } from '@/constants/messages';
import { fill } from '@/utils/format';
import { MATERIAL_CATEGORIES, MATERIAL_TYPE } from '@/constants/warehouse';
import type { SaveMaterialPayload } from '@/types/material';
import { MAX_FABRIC_WIDTH_M } from '../constants';
import type { MissingMaterial } from '../types';
import { materialTypeDefaults } from '../utils/excelReview';

const text = MESSAGES.orders.excelReview;
const col = text.materialColumns;

type MissingMaterialsPanelProps = {
  /** Gợi ý máy chủ (màu áo đang dùng mã) để gợi ý ô Màu. */
  missing: MissingMaterial[];
  materials: SaveMaterialPayload[];
  onChange: (code: string, patch: Partial<SaveMaterialPayload>) => void;
};

/**
 * DEV-ALL-123: cảnh báo NVL trong file chưa có trong danh mục kho, kèm bảng điền thông tin để thêm vào danh mục khi lưu (nếu
 * admin chọn thêm lúc Xác nhận). Không chặn: các dòng dùng NVL này vẫn được giữ.
 */
export function MissingMaterialsPanel({
  missing,
  materials,
  onChange,
}: MissingMaterialsPanelProps) {
  const colorsOf = (code: string) =>
    (missing.find((m) => m.materialCode === code)?.colors ?? []).map((c) => ({ value: c }));

  const columns: ColumnsType<SaveMaterialPayload> = [
    { title: col.code, dataIndex: 'materialCode', width: 110 },
    {
      title: col.name,
      key: 'name',
      width: 200,
      render: (_, m) => (
        <Input
          status={m.materialName.trim() ? undefined : 'error'}
          value={m.materialName}
          placeholder={col.name}
          onChange={(e) => onChange(m.materialCode ?? '', { materialName: e.target.value })}
        />
      ),
    },
    {
      title: col.type,
      key: 'type',
      width: 110,
      render: (_, m) => (
        <Select
          style={{ width: '100%' }}
          value={m.materialType}
          options={Object.entries(text.materialTypes).map(([value, label]) => ({ value, label }))}
          onChange={(materialType: string) =>
            onChange(m.materialCode ?? '', materialTypeDefaults(materialType, m.widthM))
          }
        />
      ),
    },
    {
      title: col.category,
      key: 'category',
      width: 130,
      render: (_, m) => (
        <Select
          style={{ width: '100%' }}
          value={m.category}
          options={MATERIAL_CATEGORIES[m.materialType] ?? []}
          onChange={(category: string) => onChange(m.materialCode ?? '', { category })}
        />
      ),
    },
    {
      title: col.unit,
      key: 'unit',
      width: 90,
      render: (_, m) => (
        <Input
          value={m.unit}
          disabled={m.materialType === MATERIAL_TYPE.FABRIC}
          onChange={(e) => onChange(m.materialCode ?? '', { unit: e.target.value })}
        />
      ),
    },
    {
      title: col.color,
      key: 'color',
      width: 120,
      render: (_, m) => (
        <AutoComplete
          style={{ width: '100%' }}
          value={m.color}
          options={colorsOf(m.materialCode ?? '')}
          onChange={(color: string) => onChange(m.materialCode ?? '', { color })}
        />
      ),
    },
    {
      title: col.width,
      key: 'width',
      width: 100,
      render: (_, m) =>
        m.materialType === MATERIAL_TYPE.FABRIC ? (
          <InputNumber
            min={0.1}
            max={MAX_FABRIC_WIDTH_M}
            step={0.1}
            status={m.widthM ? undefined : 'error'}
            value={m.widthM}
            onChange={(widthM) => onChange(m.materialCode ?? '', { widthM })}
          />
        ) : (
          '—'
        ),
    },
  ];

  return (
    <Alert
      type="warning"
      showIcon
      message={fill(text.missingTitle, materials.length)}
      description={
        <>
          <Typography.Paragraph style={{ marginBottom: 8 }}>
            {text.missingHint}
          </Typography.Paragraph>
          <Table<SaveMaterialPayload>
            rowKey={(m) => m.materialCode ?? ''}
            size="small"
            pagination={false}
            columns={columns}
            dataSource={materials}
            scroll={{ x: 'max-content' }}
          />
        </>
      }
    />
  );
}
