import type { TableProps } from 'antd';

/** Bảng mang class này được xếp thành thẻ trên điện thoại của vai trò xưởng (styles/workshop-mobile.css). */
export const CARD_TABLE_CLASS = 'card-table';

/** Thẻ số liệu gọn trên điện thoại, cho bảng có cột đầu là tên và cột cuối là thao tác. */
export const STAT_CARD_CLASS = 'stat-cards';

/** Ô nội dung dài (lý do, ghi chú) trong thẻ gọn: chiếm cả dòng, nhãn ở trên, nội dung trong khung. */
export const CARD_WIDE_CELL_CLASS = 'card-wide-cell';

type Column<T> = NonNullable<TableProps<T>['columns']>[number];

const withLabel = <T>(column: Column<T>): Column<T> => {
  if ('children' in column) return { ...column, children: column.children.map(withLabel) };
  const label = typeof column.title === 'string' ? column.title : undefined;
  return {
    ...column,
    onCell: (record: T, index?: number) => {
      const cell = { ...column.onCell?.(record, index), 'data-label': label };
      return cell;
    },
  };
};

/** Gắn tên cột vào từng ô (`data-label`) để khi bảng xếp thành thẻ, mỗi hàng trong thẻ vẫn có nhãn. */
export const withCardLabels = <T>(columns: TableProps<T>['columns']) => columns?.map(withLabel);
