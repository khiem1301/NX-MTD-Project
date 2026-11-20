import { DeleteOutlined } from '@ant-design/icons';
import { Input, Select, Tag, Typography, type TableColumnsType } from 'antd';
import { ConfirmAction } from '@/components/common/ConfirmAction';
import { IconAction } from '@/components/common/IconAction';
import { MESSAGES } from '@/constants/messages';
import { REASON_MAX_LENGTH } from '@/constants/limits';
import { fill } from '@/utils/format';
import { AI_LABELS, GR_LABELS } from '../constants';
import type { GradeSuggestionItem, UpdateSuggestionItemPayload } from '../types';
import { isChanged } from '../utils/gradeSuggestion';
import { SuggestionMetrics } from './SuggestionMetrics';

type Args = {
  gradeOptions: { value: string; label: string }[];
  disabled: boolean;
  onSave: (workerId: number, body: UpdateSuggestionItemPayload) => void;
  /** Bỏ dòng khỏi bản gợi ý; trả Promise để hộp xác nhận hiện trạng thái đang xử lý. */
  onRemove: (workerId: number) => Promise<unknown>;
};

/** Xếp các phần trong ô theo chiều dọc và giãn hết chiều rộng ô (Space làm ô lý do co theo nội dung). */
const STACK = { display: 'flex', flexDirection: 'column', gap: 4, width: '100%' } as const;
const SMALL = { fontSize: 12 } as const;
const TAG = { marginInlineEnd: 0 } as const;

const saveReason = (item: GradeSuggestionItem, text: string, onSave: Args['onSave']) => {
  const reason = text.trim();
  if (reason && reason !== item.reason && item.proposedGradeCode)
    onSave(item.workerId, { proposedGradeCode: item.proposedGradeCode, reason });
};

/**
 * Cột bảng gợi ý AI dạng bản xem trước sửa trực tiếp: đổi bậc là lưu ngay, gõ lý do rồi rời ô là lưu, không cần bấm nút.
 */
export const aiSuggestionColumns = ({
  gradeOptions,
  disabled,
  onSave,
  onRemove,
}: Args): TableColumnsType<GradeSuggestionItem> => [
  {
    title: GR_LABELS.worker,
    key: 'worker',
    width: 190,
    render: (_, i) => (
      <div style={STACK}>
        <Typography.Text strong>{i.fullName}</Typography.Text>
        <Typography.Text type="secondary" style={SMALL}>
          {i.workerCode}
        </Typography.Text>
      </div>
    ),
  },
  {
    title: GR_LABELS.currentGrade,
    key: 'current',
    width: 110,
    align: 'center',
    render: (_, i) => (
      <div style={{ ...STACK, alignItems: 'center' }}>
        <Tag style={TAG}>{i.currentGradeCode ?? GR_LABELS.none}</Tag>
        {i.pendingGradeCode && (
          <Tag color="gold" style={TAG}>
            {fill(AI_LABELS.pendingTag, i.pendingGradeCode)}
          </Tag>
        )}
      </div>
    ),
  },
  {
    title: AI_LABELS.metrics,
    key: 'metrics',
    width: 230,
    render: (_, i) => <SuggestionMetrics metrics={i.metrics} />,
  },
  {
    title: AI_LABELS.aiGrade,
    dataIndex: 'aiGradeCode',
    width: 80,
    align: 'center',
    render: (v: string | null) =>
      v ? (
        <Tag color="purple" style={TAG}>
          {v}
        </Tag>
      ) : (
        '—'
      ),
  },
  {
    title: GR_LABELS.proposedGrade,
    key: 'proposed',
    width: 190,
    render: (_, i) => (
      <Select
        value={i.proposedGradeCode ?? undefined}
        options={gradeOptions}
        disabled={disabled}
        popupMatchSelectWidth={false}
        style={{ width: '100%' }}
        onChange={(code: string) =>
          onSave(i.workerId, { proposedGradeCode: code, reason: i.reason })
        }
      />
    ),
  },
  {
    title: GR_LABELS.reason,
    key: 'reason',
    render: (_, i) => (
      <div style={STACK}>
        <Input.TextArea
          // Đổi key khi lý do đã lưu thay đổi (tạo lại, sửa từ máy khác) để ô hiện đúng giá trị mới.
          key={i.reason}
          defaultValue={i.reason}
          autoSize={{ minRows: 2, maxRows: 6 }}
          maxLength={REASON_MAX_LENGTH}
          disabled={disabled || !i.proposedGradeCode}
          onBlur={(e) => saveReason(i, e.target.value, onSave)}
        />
        {i.isEdited && i.aiReason !== i.reason && (
          <Typography.Text type="secondary" style={SMALL}>
            {fill(AI_LABELS.aiReason, i.aiReason)}
          </Typography.Text>
        )}
      </div>
    ),
  },
  {
    title: AI_LABELS.change,
    key: 'change',
    width: 110,
    align: 'center',
    render: (_, i) => (
      <div style={{ ...STACK, alignItems: 'center' }}>
        {isChanged(i) ? (
          <Tag color="blue" style={TAG}>
            {AI_LABELS.changed}
          </Tag>
        ) : (
          <Tag style={TAG}>{AI_LABELS.unchanged}</Tag>
        )}
        {i.isEdited && (
          <Tag color="orange" style={TAG}>
            {AI_LABELS.edited}
          </Tag>
        )}
      </div>
    ),
  },
  {
    title: MESSAGES.common.actions,
    key: 'actions',
    width: 70,
    render: (_, i) => (
      <ConfirmAction
        title={fill(AI_LABELS.removeTitle, i.fullName)}
        description={AI_LABELS.removeBody}
        disabled={disabled}
        onConfirm={() => onRemove(i.workerId)}
      >
        <IconAction title={AI_LABELS.remove} icon={<DeleteOutlined />} danger disabled={disabled} />
      </ConfirmAction>
    ),
  },
];
