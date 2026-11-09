import { useState } from 'react';
import { Alert, Button, Select, Space, Typography, message } from 'antd';
import { fill } from '@/utils/format';
import { ALLOT_MAX_LINES, PRODUCTION_TEXT } from '../constants';
import { useAddOrderLineTeams } from '../hooks/useOrderLineTeams';

const text = PRODUCTION_TEXT.orderLineTeams;

type OrderLineTeamBulkBarProps = {
  orderLineIds: number[];
  teamOptions: { value: number; label: string }[];
  onDone: () => void;
};

/** Hiện khi đã tích chọn dòng đơn, dính ở cuối màn hình: chia tất cả dòng đã chọn cho thêm tổ, tổ đang được chia vẫn giữ nguyên. */
export function OrderLineTeamBulkBar({
  orderLineIds,
  teamOptions,
  onDone,
}: OrderLineTeamBulkBarProps) {
  const [teamIds, setTeamIds] = useState<number[]>([]);
  const add = useAddOrderLineTeams();
  const tooMany = orderLineIds.length > ALLOT_MAX_LINES;

  const apply = () =>
    add.mutate(
      { orderLineIds, teamIds },
      {
        onSuccess: (added) => {
          if (added === 0) void message.info(text.bulkNone);
          else void message.success(fill(text.bulkDone, added));
          setTeamIds([]);
          onDone();
        },
      },
    );

  return (
    <Alert
      type="info"
      style={{
        position: 'sticky',
        bottom: 16,
        zIndex: 3,
        marginTop: 12,
        boxShadow: '0 4px 12px rgba(0, 0, 0, 0.15)',
      }}
      message={
        <Space wrap>
          {fill(text.selectedCount, orderLineIds.length)}
          <Select<number[]>
            mode="multiple"
            optionFilterProp="label"
            style={{ width: 280 }}
            placeholder={text.bulkTeams}
            aria-label={text.bulkTeams}
            options={teamOptions}
            value={teamIds}
            onChange={setTeamIds}
          />
          <Button
            type="primary"
            disabled={teamIds.length === 0 || tooMany}
            loading={add.isPending}
            onClick={apply}
          >
            {text.bulkApply}
          </Button>
          <Button disabled={add.isPending} onClick={onDone}>
            {text.bulkClear}
          </Button>
          {tooMany && (
            <Typography.Text type="danger">
              {fill(text.tooManySelected, ALLOT_MAX_LINES)}
            </Typography.Text>
          )}
        </Space>
      }
    />
  );
}
