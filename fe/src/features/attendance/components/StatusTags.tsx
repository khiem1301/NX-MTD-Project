import { Space, Tag } from 'antd';
import { ATTENDANCE_TEXT } from '../constants';
import type { DailyTimesheet } from '../types';

type TagItem = { color: string; text: string };

export function StatusTags({ row }: { row: DailyTimesheet }) {
  const tags: TagItem[] = [];
  if (row.isMissingPunch) tags.push({ color: 'orange', text: ATTENDANCE_TEXT.tag.missingPunch });
  if (row.hasExtraPunch) tags.push({ color: 'gold', text: ATTENDANCE_TEXT.tag.extraPunch });
  if (row.source === 'ADJUSTMENT')
    tags.push({ color: 'purple', text: ATTENDANCE_TEXT.tag.adjusted });

  if (tags.length === 0) {
    return <Tag color="green">{ATTENDANCE_TEXT.tag.normal}</Tag>;
  }

  return (
    <Space size={4} wrap>
      {tags.map((tag) => (
        <Tag key={tag.text} color={tag.color}>
          {tag.text}
        </Tag>
      ))}
    </Space>
  );
}
