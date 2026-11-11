import { DeleteOutlined, PlusOutlined } from '@ant-design/icons';
import { Button, Card, Flex, Select, Typography } from 'antd';
import { DataTable } from '@/components/common/DataTable';
import { MESSAGES } from '@/constants/messages';
import { fill } from '@/utils/format';
import { MAX_MARKERS, type GroupRow, type LayoutRow, type PanelFabrics } from '../utils/markerSpec';
import { specLayoutColumns } from './specLayoutColumns';

const text = MESSAGES.orders.spec;

type SpecLayoutCardProps = {
  layout: LayoutRow;
  name: string;
  /** Màu áo của mã hàng: `value` là `colorKey(màu)`; `disabled` khi màu đã thuộc dạng bìa khác. */
  colorOptions: { value: string; label: string; disabled?: boolean }[];
  panels: { value: string; label: string }[];
  fabrics: PanelFabrics;
  colorName: (ck: string) => string;
  fabricLabel: (materialId: number) => string;
  /** Tổng số nhóm của mã hàng (mọi dạng), để chặn quá giới hạn. */
  groupCount: number;
  readOnly: boolean;
  onChange: (change: Partial<LayoutRow>) => void;
  onAddGroup: () => void;
  onRemove: () => void;
};

/**
 * Một dạng bìa (DEV-ALL-89): tiêu đề kèm các màu dùng dạng, bên dưới là bảng nhóm bìa (mỗi dòng một nhóm: chi tiết cắt và vải theo
 * từng màu của dạng).
 */
export function SpecLayoutCard({
  layout,
  name,
  colorOptions,
  panels,
  fabrics,
  colorName,
  fabricLabel,
  groupCount,
  readOnly,
  onChange,
  onAddGroup,
  onRemove,
}: SpecLayoutCardProps) {
  const patchGroup = (key: string, change: Partial<GroupRow>) =>
    onChange({ groups: layout.groups.map((g) => (g.key === key ? { ...g, ...change } : g)) });
  const columns = specLayoutColumns({
    layout,
    panels,
    fabrics,
    colorName,
    fabricLabel,
    readOnly,
    onChangeGroup: patchGroup,
    onRemoveGroup: (key) => onChange({ groups: layout.groups.filter((g) => g.key !== key) }),
  });
  const title = (
    <Flex align="center" gap="middle" wrap>
      <Typography.Text strong>{fill(text.layoutTitle, name)}</Typography.Text>
      <Select
        mode="multiple"
        size="small"
        disabled={readOnly}
        aria-label={fill(text.layoutColorsOf, name)}
        placeholder={text.layoutColorsPlaceholder}
        value={layout.colorKeys}
        options={colorOptions}
        optionFilterProp="label"
        onChange={(colorKeys: string[]) => onChange({ colorKeys })}
        style={{ flex: 1, minWidth: 240 }}
      />
    </Flex>
  );
  return (
    <Card
      size="small"
      title={title}
      extra={
        !readOnly && (
          <Button
            size="small"
            type="text"
            danger
            icon={<DeleteOutlined />}
            aria-label={fill(text.removeLayout, name)}
            onClick={onRemove}
          />
        )
      }
    >
      <DataTable<GroupRow>
        rowKey="key"
        size="small"
        pagination={false}
        dataSource={layout.groups}
        columns={columns}
        locale={{ emptyText: text.layoutNoGroups }}
      />
      {!readOnly && groupCount < MAX_MARKERS && (
        <Button type="link" size="small" icon={<PlusOutlined />} onClick={onAddGroup}>
          {text.addMarker}
        </Button>
      )}
    </Card>
  );
}
