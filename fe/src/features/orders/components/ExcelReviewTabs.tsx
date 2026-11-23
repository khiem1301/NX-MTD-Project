import { Badge, Space, Tabs, theme } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { EXCEL_COMPONENT_TYPE } from '../constants';
import type { useExcelReview } from '../hooks/useExcelReview';
import { rowsOf, type ReviewTable, type RowOf } from '../utils/excelReview';
import {
  componentColumns,
  groupColumns,
  markerColumns,
  normColumns,
  orderMaterialColumns,
  productColumns,
  type ReviewColumnsCtx,
} from './excelReviewColumns';
import { ExcelReviewTable } from './ExcelReviewTable';

const text = MESSAGES.orders.excelReview;

type ExcelReviewTabsProps = {
  review: ReturnType<typeof useExcelReview>;
  tables: readonly ReviewTable[];
  active: ReviewTable | undefined;
  onActiveChange: (table: ReviewTable) => void;
  onlyErrors: boolean;
  creating: boolean;
  materialOptions: { value: string; label: string }[];
};

/** DEV-ALL-122: các tab của popup kiểm tra Excel, mỗi sheet một bảng sửa được; nhãn tab hiện số dòng lỗi (đỏ) hoặc số dòng. */
export function ExcelReviewTabs({
  review,
  tables,
  active,
  onActiveChange,
  onlyErrors,
  creating,
  materialOptions,
}: ExcelReviewTabsProps) {
  const { token } = theme.useToken();
  const { draft } = review;
  const components = draft.spec.components;
  const componentNames = components.map((c) => c.name);
  const panelNames = components
    .filter((c) => c.componentType === EXCEL_COMPONENT_TYPE.PANEL)
    .map((c) => c.name);
  // Bìa của đơn chỉ hiện size của đơn (máy chủ trả sau lần kiểm đầu); chưa có đơn / chưa kiểm thì tạm hiện size trong file.
  const orderSizes = review.data?.spec.orderSizes; // kết quả kiểm gần nhất
  const markerSizes = orderSizes && orderSizes.length > 0 ? orderSizes : draft.spec.sizeColumns;
  const colors = [
    ...draft.spec.norms.map((n) => n.color),
    ...draft.spec.markerGroups.flatMap((g) => g.colors),
  ];

  const ctx = <T extends ReviewTable>(table: T): ReviewColumnsCtx<RowOf<T>> => ({
    update: (rowNumber, patch) => review.updateRow(table, rowNumber, patch),
    remove: (rowNumber) => review.removeRow(table, rowNumber),
    status: review.status[table],
    creating,
  });
  const common = {
    onlyErrors,
    stateOf:
      <T extends ReviewTable>(table: T) =>
      (r: RowOf<T>) =>
        review.stateOf(table, r),
  };

  const tableOf = (table: ReviewTable) => {
    const props = { onlyErrors, onAdd: () => review.addRow(table) };
    switch (table) {
      case 'products':
        return (
          <ExcelReviewTable
            {...props}
            rows={draft.products}
            columns={productColumns(ctx(table))}
            stateOf={common.stateOf(table)}
          />
        );
      case 'components':
        return (
          <ExcelReviewTable
            {...props}
            rows={components}
            columns={componentColumns(ctx(table))}
            stateOf={common.stateOf(table)}
          />
        );
      case 'norms':
        return (
          <ExcelReviewTable
            {...props}
            rows={draft.spec.norms}
            columns={normColumns(ctx(table), componentNames, colors, materialOptions)}
            stateOf={common.stateOf(table)}
          />
        );
      case 'groups':
        return (
          <ExcelReviewTable
            {...props}
            rows={draft.spec.markerGroups}
            columns={groupColumns(ctx(table), panelNames, colors)}
            stateOf={common.stateOf(table)}
          />
        );
      case 'markers':
        return (
          <ExcelReviewTable
            {...props}
            rows={draft.spec.orderMarkers}
            columns={markerColumns(ctx(table), markerSizes)}
            stateOf={common.stateOf(table)}
          />
        );
      case 'orderMaterials':
        return (
          <ExcelReviewTable
            {...props}
            rows={draft.orderMaterials}
            columns={orderMaterialColumns(ctx(table), materialOptions)}
            stateOf={common.stateOf(table)}
          />
        );
    }
  };

  const items = tables.map((table) => {
    const rows = rowsOf(draft, table);
    const errors = rows.filter((r) => review.stateOf(table, r) === 'error').length;
    return {
      key: table,
      label: (
        <Space size={6}>
          {text.tabs[table]}
          <Badge
            count={errors > 0 ? errors : rows.length}
            color={errors > 0 ? token.colorError : token.colorSuccess}
            showZero
          />
        </Space>
      ),
      children: tableOf(table),
    };
  });

  return (
    <Tabs
      activeKey={active}
      onChange={(k) => {
        const table = tables.find((t) => t === k);
        if (table) onActiveChange(table);
      }}
      items={items}
      size="small"
    />
  );
}
