import { Select } from 'antd';
import { fill } from '@/utils/format';
import { PRODUCTION_TEXT } from '../constants';
import { useSetOrderLineTeams } from '../hooks/useProduction';
import type { OrderLineTeamRow } from '../types';

const text = PRODUCTION_TEXT.orderLineTeams;

type OrderLineTeamSelectProps = {
  row: OrderLineTeamRow;
  teamOptions: { value: number; label: string }[];
  teamsLoading: boolean;
};

/**
 * Ô chọn tổ may của một dòng đơn: chọn thêm là chia, bỏ chọn là bỏ chia. Lỗi thì ô giữ nguyên theo dữ liệu đã lưu.
 * API nhận cả danh sách tổ nên khóa ô khi đang lưu, để lần chọn sau không ghi đè bằng danh sách cũ.
 */
export function OrderLineTeamSelect({ row, teamOptions, teamsLoading }: OrderLineTeamSelectProps) {
  const save = useSetOrderLineTeams();
  const known = new Set(teamOptions.map((o) => o.value));
  const gone = row.teams
    .filter((t) => !known.has(t.teamId))
    .map((t) => ({ value: t.teamId, label: fill(text.teamGone, t.teamName) }));

  return (
    <Select<number[]>
      mode="multiple"
      style={{ width: '100%', minWidth: 240 }}
      optionFilterProp="label"
      placeholder={text.teamPlaceholder}
      aria-label={fill(text.teamAria, row.orderCode, row.productCode, row.color, row.sizeCode)}
      options={[...teamOptions, ...gone]}
      value={row.teams.map((t) => t.teamId)}
      loading={teamsLoading || save.isPending}
      disabled={save.isPending}
      onChange={(teamIds) => save.mutate({ orderLineId: row.orderLineId, teamIds })}
    />
  );
}
