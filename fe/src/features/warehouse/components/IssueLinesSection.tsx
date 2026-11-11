import type { ReactNode } from 'react';
import { PlusOutlined } from '@ant-design/icons';
import { Button, Flex, Space, Typography } from 'antd';
import { fill } from '@/utils/format';
import { LABELS } from '../constants';

type IssueLinesSectionProps = {
  /** Số dòng đang có và số dòng đã nhập số cấp. */
  total: number;
  filled: number;
  addLabel: string;
  addDisabled: boolean;
  onAdd: () => void;
  /** Thanh đề xuất số cấp (theo lệnh cắt / mã hàng). */
  suggestion?: ReactNode;
  children: ReactNode;
};

/** Khung dòng cấp: tiêu đề kèm số dòng đã nhập, nút thêm dòng ở góc phải, thanh đề xuất rồi tới bảng dòng. */
export function IssueLinesSection({
  total,
  filled,
  addLabel,
  addDisabled,
  onAdd,
  suggestion,
  children,
}: IssueLinesSectionProps) {
  return (
    <>
      <Flex justify="space-between" align="center" wrap gap={8} style={{ marginBottom: 8 }}>
        <Space size={8}>
          <Typography.Text strong>{LABELS.issueLinesTitle}</Typography.Text>
          {total > 0 && (
            <Typography.Text type="secondary">
              {fill(LABELS.issueLinesFilled, filled, total)}
            </Typography.Text>
          )}
        </Space>
        <Button icon={<PlusOutlined />} disabled={addDisabled} onClick={onAdd}>
          {addLabel}
        </Button>
      </Flex>
      {suggestion}
      {children}
    </>
  );
}
