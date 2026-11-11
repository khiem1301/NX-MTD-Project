import { Select, Typography, type TableColumnsType } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { fill } from '@/utils/format';
import {
  componentBlock,
  markerFabricsOf,
  type GroupRow,
  type LayoutRow,
  type PanelFabrics,
} from '../utils/markerSpec';
import { deleteRowColumn } from './deleteRowColumn';

const text = MESSAGES.orders.spec;

/** Bề rộng cột (px): số nhóm, vải của mỗi màu. */
const WIDTH = { no: 72, fabric: 180 } as const;

type SpecLayoutColumnsOptions = {
  layout: LayoutRow;
  /** Chi tiết cắt (Panel) của mã hàng: `value` là `ComponentRow.key`. */
  panels: { value: string; label: string }[];
  fabrics: PanelFabrics;
  colorName: (ck: string) => string;
  fabricLabel: (materialId: number) => string;
  readOnly: boolean;
  onChangeGroup: (key: string, change: Partial<GroupRow>) => void;
  onRemoveGroup: (key: string) => void;
};

/** Chi tiết chọn được cho nhóm; chi tiết đã ở nhóm khác của dạng hoặc khác vải bị khóa kèm lý do. */
const componentOptions = (o: SpecLayoutColumnsOptions, group: GroupRow) =>
  o.panels.map((p) => {
    if (group.componentKeys.includes(p.value)) return p;
    const block = componentBlock(o.layout, group, p.value, o.fabrics);
    if (block === undefined) return p;
    const label =
      'otherGroup' in block
        ? fill(text.markerComponentInOtherGroup, p.label, block.otherGroup)
        : fill(text.markerComponentOtherFabric, p.label, o.colorName(block.colorKey));
    return { ...p, label, disabled: true };
  });

/**
 * Cột bảng nhóm bìa của một dạng bìa (DEV-ALL-89): số nhóm, chi tiết cắt chung bìa, vải của nhóm ở từng màu của dạng (lấy từ định
 * mức, chỉ xem; đỏ khi chưa có vải hoặc các chi tiết khác vải).
 */
export const specLayoutColumns = (o: SpecLayoutColumnsOptions): TableColumnsType<GroupRow> => [
  { title: text.markerNo, key: 'no', width: WIDTH.no, render: (_, g) => g.markerNo },
  {
    title: text.markerComponents,
    key: 'components',
    render: (_, g) => (
      <Select
        mode="multiple"
        size="small"
        disabled={o.readOnly}
        aria-label={fill(text.markerComponentsOf, g.markerNo)}
        placeholder={text.markerComponentsPlaceholder}
        value={g.componentKeys}
        options={componentOptions(o, g)}
        optionFilterProp="label"
        onChange={(componentKeys: string[]) => o.onChangeGroup(g.key, { componentKeys })}
        style={{ width: '100%' }}
      />
    ),
  },
  ...o.layout.colorKeys.map((ck) => ({
    title: fill(text.markerFabricOf, o.colorName(ck)),
    key: `fabric-${ck}`,
    width: WIDTH.fabric,
    render: (_: unknown, g: GroupRow) => {
      const ids = markerFabricsOf(g.componentKeys, ck, o.fabrics);
      if (ids.length === 0)
        return <Typography.Text type="danger">{text.markerFabricNone}</Typography.Text>;
      return (
        <Typography.Text type={ids.length > 1 ? 'danger' : undefined}>
          {ids.map(o.fabricLabel).join(', ')}
        </Typography.Text>
      );
    },
  })),
  ...(o.readOnly ? [] : [{ ...deleteRowColumn<GroupRow>(o.onRemoveGroup), fixed: undefined }]),
];
