import { Badge, Space, Tabs, theme } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { EXCEL_COMPONENT_TYPE } from '../constants';
import type { useExcelReview } from '../hooks/useExcelReview';
import type { ReviewDbOptions } from '../hooks/useExistingData';
import { rowsOf, type ReviewTable, type RowOf } from '../utils/excelReview';
import {
  componentColumns,
  groupColumns,
  markerColumns,
  materialSheetColumns,
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
  /** Chi tiết / màu đang có ở mã hàng trong hệ thống: gợi ý chọn ở các ô, cộng với những gì trong bảng. */
  dbOptions?: ReviewDbOptions;
  /** Mở lại NVL ngừng sử dụng (chỉ Admin); không truyền thì dòng lỗi này không có nút. */
  reopenMaterial?: (code: string) => void;
  reopeningCode?: string;
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
  dbOptions,
  reopenMaterial,
  reopeningCode,
}: ExcelReviewTabsProps) {
  const { token } = theme.useToken();
  const { draft } = review;
  const components = draft.spec.components;
  const componentNames = [
    ...components.map((c) => c.name),
    ...(dbOptions?.components ?? []).map((c) => c.name),
  ];
  const panelNames = [
    ...components.filter((c) => c.componentType === EXCEL_COMPONENT_TYPE.PANEL).map((c) => c.name),
    ...(dbOptions?.components ?? []).filter((c) => c.panel).map((c) => c.name),
  ];
  // Bìa của đơn chỉ hiện size của đơn (máy chủ trả sau lần kiểm đầu); chưa có đơn / chưa kiểm thì tạm hiện size trong file.
  const orderSizes = review.data?.spec.orderSizes; // kết quả kiểm gần nhất
  const markerSizes = orderSizes && orderSizes.length > 0 ? orderSizes : draft.spec.sizeColumns;
  const colors = [
    ...draft.spec.norms.map((n) => n.color),
    ...draft.spec.markerGroups.flatMap((g) => g.colors),
    ...(dbOptions?.colors ?? []),
  ];

  const ctx = <T extends ReviewTable>(table: T): ReviewColumnsCtx<RowOf<T>> => ({
    update: (rowNumber, patch) => review.updateRow(table, rowNumber, patch),
    remove: (rowNumber) => review.removeRow(table, rowNumber),
    status: review.status[table],
    creating,
    ...(table === 'norms' || table === 'orderMaterials'
      ? {
          materialCodeOf: (r: RowOf<T>) => (r as { materialCode: string }).materialCode,
          reopenMaterial,
          reopeningCode,
        }
      : {}),
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
            // Mỗi lần nhập chỉ một mã hàng (mã đang tạo ở form).
            addDisabled={draft.products.length >= 1}
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
      case 'materials':
        return (
          <ExcelReviewTable
            {...props}
            rows={draft.materials}
            columns={materialSheetColumns(ctx(table))}
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
