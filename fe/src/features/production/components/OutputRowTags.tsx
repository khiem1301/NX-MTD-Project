import { Space, Tag } from 'antd';
import { PRODUCTION_TEXT } from '../constants';
import type { OutputGridRow } from '../types';

const text = PRODUCTION_TEXT.output;

/** Nhãn cảnh báo của một dòng sản lượng: ngoài phân công, chưa chấm công, chưa có đơn giá, đã khóa. */
export function OutputRowTags({ row }: { row: OutputGridRow }) {
  return (
    <Space size={4} wrap>
      {row.isOutsideAssignment && <Tag color="orange">{text.tags.outside}</Tag>}
      {row.noAttendanceWarning && <Tag color="orange">{text.tags.noAttendance}</Tag>}
      {!row.rateApproved && <Tag color="red">{text.tags.noRate}</Tag>}
      {row.quantity !== null && !row.canEdit && <Tag>{text.tags.locked}</Tag>}
    </Space>
  );
}
