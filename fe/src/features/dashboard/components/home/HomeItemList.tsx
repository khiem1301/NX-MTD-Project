import type { CSSProperties, ReactNode } from 'react';
import { Card, Empty, List, Tag, Typography } from 'antd';
import { HOME_ROLE_TEXT } from '../../constants';
import type { HomeItem } from '../../types';

/** Chuyển màu từ BE (gray) sang tên màu của antd Tag. */
const tagColor = (color: string) => (color === 'gray' ? 'default' : color);

/** Danh sách các dòng chờ xử lý của trang chủ: tiêu đề, dòng phụ và nhãn màu. Bấm cả danh sách mở màn nghiệp vụ (nếu có). */
export function HomeItemList({
  title,
  items,
  onOpen,
  extra,
  bodyMaxHeight,
  style,
}: {
  title: ReactNode;
  items: HomeItem[];
  onOpen?: () => void;
  extra?: ReactNode;
  /** Giới hạn chiều cao phần danh sách, cuộn khi dài (tránh đẩy khối dưới ra khỏi viewport). */
  bodyMaxHeight?: number | string;
  style?: CSSProperties;
}) {
  const bodyStyle: CSSProperties | undefined = bodyMaxHeight
    ? { maxHeight: bodyMaxHeight, overflowY: 'auto' }
    : undefined;
  return (
    <Card title={title} size="small" style={{ height: '100%', ...style }} styles={{ body: bodyStyle }}>
      {extra}
      {items.length === 0 ? (
        <Empty
          image={Empty.PRESENTED_IMAGE_SIMPLE}
          description={HOME_ROLE_TEXT.empty}
          style={{ marginBlock: 8 }}
        />
      ) : (
        <List
          size="small"
          dataSource={items}
          renderItem={(item) => (
            <List.Item
              style={{ cursor: onOpen ? 'pointer' : 'default', paddingBlock: 8 }}
              onClick={onOpen}
              extra={<Tag color={tagColor(item.tagColor)}>{item.tag}</Tag>}
            >
              <List.Item.Meta
                title={item.title}
                description={
                  item.meta && (
                    <Typography.Text
                      type={item.tagColor === 'red' ? 'danger' : 'secondary'}
                      style={{ fontSize: 12 }}
                    >
                      {item.meta}
                    </Typography.Text>
                  )
                }
              />
            </List.Item>
          )}
        />
      )}
    </Card>
  );
}
