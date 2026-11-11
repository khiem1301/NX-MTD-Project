import type { Dispatch, SetStateAction } from 'react';
import { PlusOutlined, ThunderboltOutlined } from '@ant-design/icons';
import { Button, Empty, Flex, Space, Typography } from 'antd';
import { MESSAGES } from '@/constants/messages';
import {
  distinctColors,
  emptyGroup,
  emptyLayout,
  layoutName,
  panelFabrics,
  suggestLayouts,
  type LayoutRow,
} from '../utils/markerSpec';
import {
  colorKey,
  dropRow,
  patchRow,
  type ComponentRow,
  type MaterialOption,
  type NormRow,
  type VariantRow,
} from '../utils/productSpec';
import { SpecLayoutCard } from './SpecLayoutCard';

const text = MESSAGES.orders.spec;

type SpecMarkersTabProps = {
  layouts: LayoutRow[];
  setLayouts: Dispatch<SetStateAction<LayoutRow[]>>;
  variants: VariantRow[];
  norms: NormRow[];
  components: ComponentRow[];
  materialOptions: MaterialOption[];
  /** Chế độ chỉ xem. Kế toán và Admin đều sửa được nên màn mã hàng không bật. */
  readOnly?: boolean;
};

/**
 * Tab "Nhóm bìa" (DEV-ALL-85, DEV-ALL-89): các dạng bìa, mỗi dạng gồm màu áo dùng chung bộ nhóm bìa và các nhóm (chi tiết cắt chung bìa).
 * Vải theo màu của nhóm lấy từ định mức. Bìa cụ thể (dài, tỉ lệ size) khai ở đơn hàng theo nhóm.
 */
export function SpecMarkersTab({
  layouts,
  setLayouts,
  variants,
  norms,
  components,
  materialOptions,
  readOnly = false,
}: SpecMarkersTabProps) {
  const colors = distinctColors(variants);
  const names = new Map(colors.map((c) => [colorKey(c), c]));
  const colorName = (ck: string) => names.get(ck) ?? ck;
  const panels = components
    .filter((c) => c.componentType === 'Panel' && c.componentName.trim())
    .map((c) => ({ value: c.key, label: c.componentName.trim() }));
  const fabrics = panelFabrics(norms, components, materialOptions);
  const labelById = new Map(materialOptions.map((m) => [m.value, m.short]));
  const fabricLabel = (id: number) => labelById.get(id) ?? `#${id}`;
  const groupCount = layouts.flatMap((l) => l.groups).length;
  const colorOptions = (layout: LayoutRow) => {
    const taken = new Set(layouts.filter((l) => l.key !== layout.key).flatMap((l) => l.colorKeys));
    return colors.map((c) => ({ value: colorKey(c), label: c, disabled: taken.has(colorKey(c)) }));
  };

  return (
    <>
      <Typography.Paragraph type="secondary">{text.markerHint}</Typography.Paragraph>
      {layouts.length === 0 && (
        <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={text.markerEmpty} />
      )}
      <Flex vertical gap={12}>
        {layouts.map((layout, index) => (
          <SpecLayoutCard
            key={layout.key}
            layout={layout}
            name={layoutName(index)}
            colorOptions={colorOptions(layout)}
            panels={panels}
            fabrics={fabrics}
            colorName={colorName}
            fabricLabel={fabricLabel}
            groupCount={groupCount}
            readOnly={readOnly}
            onChange={(change) => patchRow(setLayouts, layout.key, change)}
            onAddGroup={() =>
              patchRow(setLayouts, layout.key, { groups: [...layout.groups, emptyGroup(layouts)] })
            }
            onRemove={() => dropRow(setLayouts, layout.key)}
          />
        ))}
      </Flex>
      {!readOnly && (
        <Space style={{ marginTop: 12 }}>
          <Button
            type="dashed"
            icon={<PlusOutlined />}
            onClick={() => setLayouts((rows) => [...rows, emptyLayout(rows, variants)])}
          >
            {text.addLayout}
          </Button>
          {layouts.length === 0 && (
            <Button
              icon={<ThunderboltOutlined />}
              onClick={() => setLayouts(suggestLayouts(variants, fabrics))}
            >
              {text.suggestLayouts}
            </Button>
          )}
        </Space>
      )}
    </>
  );
}
