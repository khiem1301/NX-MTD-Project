import { Progress, Typography } from 'antd';
import { fill } from '@/utils/format';
import { PRODUCTION_TEXT } from '../constants';

const text = PRODUCTION_TEXT.assignments;

/** Tiến độ phân công của đơn: đủ người ở mọi công đoạn thì tổ mới nhận được BTP. */
export function AssignmentProgress({ total, missing }: { total: number; missing: number }) {
  const covered = total - missing;
  return (
    <div style={{ margin: '12px 0' }}>
      <Typography.Text type={missing === 0 ? 'success' : 'warning'} strong>
        {missing === 0
          ? fill(text.coverageDone, total)
          : fill(text.coverageMissing, missing, total)}
      </Typography.Text>
      <Progress
        percent={Math.floor((covered / total) * 100)}
        format={() => `${covered}/${total}`}
        status={missing === 0 ? 'success' : 'active'}
      />
    </div>
  );
}
