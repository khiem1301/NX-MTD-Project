import { useMemo, useState } from 'react';
import { ReloadOutlined, RobotOutlined, SendOutlined } from '@ant-design/icons';
import { Button, Empty, Modal, Space, Spin, Typography, message } from 'antd';
import { DataTable } from '@/components/common/DataTable';
import { QueryError } from '@/components/common/QueryError';
import { MESSAGES } from '@/constants/messages';
import { fill } from '@/utils/format';
import { AI_LABELS, AI_LOOKBACK, AI_MESSAGES } from '../constants';
import { useGradeOptions } from '../hooks/useGrade';
import {
  useConfirmSuggestion,
  useGenerateSuggestion,
  useGradeSuggestion,
  useRemoveSuggestionItem,
  useUpdateSuggestionItem,
} from '../hooks/useGradeSuggestion';
import type { GradeSuggestionConfirmResult } from '../types';
import {
  confirmCounts,
  filterSuggestions,
  periodDays,
  type SuggestionFilters,
} from '../utils/gradeSuggestion';
import { aiSuggestionColumns } from './aiSuggestionColumns';
import { AiSuggestionToolbar } from './AiSuggestionToolbar';
import { LookbackDaysInput } from './LookbackDaysInput';

const MODAL_WIDTH = 1360;
const TABLE_MIN_WIDTH = 1200;
// Cả tổ hiện trên một bảng (không phân trang), cuộn dọc trong modal, tiêu đề cột đứng yên.
const TABLE_HEIGHT = 'calc(100vh - 360px)';
const FOOTER = {
  display: 'flex',
  justifyContent: 'space-between',
  alignItems: 'center',
  gap: 8,
  flexWrap: 'wrap',
} as const;

const showResult = (r: GradeSuggestionConfirmResult) => {
  const sent = r.created + r.replaced;
  void message.success(
    sent > 0 ? fill(AI_MESSAGES.confirmed, sent, r.created, r.replaced) : AI_MESSAGES.noChange,
  );
  if (r.skipped.length > 0)
    Modal.warning({
      title: AI_LABELS.skippedTitle,
      content: (
        <ul>
          {r.skipped.map((s) => (
            <li key={s}>{s}</li>
          ))}
        </ul>
      ),
    });
};

/**
 * DEV-ALL-148: AI gợi ý bậc cho mọi công nhân của tổ. Bản gợi ý được lưu tạm ở máy chủ (đóng modal mở lại vẫn còn,
 * mỗi lần sửa được lưu); xác nhận thì chỉ dòng đổi bậc thành đề xuất, đề xuất đang chờ của công nhân được thay bằng bản mới.
 */
export function AiSuggestionModal({ open, onClose }: { open: boolean; onClose: () => void }) {
  const query = useGradeSuggestion(open);
  const generate = useGenerateSuggestion();
  const update = useUpdateSuggestionItem();
  const removeItem = useRemoveSuggestionItem();
  const confirm = useConfirmSuggestion();
  const gradeOptions = useGradeOptions();
  const [filters, setFilters] = useState<SuggestionFilters>({});
  const [days, setDays] = useState<number | null>(null);
  const draft = query.data ?? null;
  // Chưa chọn thì lấy đúng kỳ của bản gợi ý đang có, chưa có bản nào thì mặc định.
  const lookback =
    days ?? (draft ? periodDays(draft.periodFrom, draft.periodTo) : AI_LOOKBACK.DEFAULT);
  const rows = useMemo(() => filterSuggestions(draft?.items ?? [], filters), [draft, filters]);
  const counts = confirmCounts(draft?.items ?? []);
  const busy = generate.isPending || confirm.isPending;

  const handleRegenerate = () =>
    Modal.confirm({
      title: AI_LABELS.regenerateTitle,
      content: fill(AI_LABELS.regenerateBody, lookback),
      okText: AI_LABELS.regenerate,
      cancelText: MESSAGES.common.cancel,
      onOk: () => generate.generate(lookback),
    });

  const handleConfirm = () =>
    Modal.confirm({
      title: AI_LABELS.confirmTitle,
      content: fill(AI_LABELS.confirmBody, counts.sent, counts.replaced),
      okText: MESSAGES.common.confirm,
      cancelText: MESSAGES.common.cancel,
      onOk: () =>
        confirm
          .mutateAsync(undefined)
          .then(showResult)
          .catch(() => undefined),
    });

  const renderBody = () => {
    if (generate.isPending)
      return (
        <Spin tip={AI_LABELS.generating} size="large">
          <div style={{ minHeight: 240 }} />
        </Spin>
      );
    if (query.isLoading) return <Spin style={{ display: 'block', padding: 48 }} />;
    if (!draft)
      return (
        <Empty description={<Typography.Text>{AI_LABELS.emptyHint}</Typography.Text>}>
          <Space wrap>
            <LookbackDaysInput value={lookback} onChange={setDays} />
            <Button
              type="primary"
              icon={<RobotOutlined />}
              onClick={() => generate.generate(lookback)}
            >
              {AI_LABELS.generate}
            </Button>
          </Space>
        </Empty>
      );
    return (
      <>
        <AiSuggestionToolbar
          draft={draft}
          filters={filters}
          gradeOptions={gradeOptions.options}
          onFiltersChange={setFilters}
        />
        <DataTable
          rowKey="workerId"
          size="small"
          columns={aiSuggestionColumns({
            gradeOptions: gradeOptions.options,
            disabled: busy,
            onSave: update.save,
            onRemove: removeItem.remove,
          })}
          dataSource={rows}
          pagination={false}
          sticky
          scroll={{ x: TABLE_MIN_WIDTH, y: TABLE_HEIGHT }}
        />
      </>
    );
  };

  return (
    <Modal
      open={open}
      title={AI_LABELS.title}
      width={MODAL_WIDTH}
      style={{ top: 24 }}
      onCancel={onClose}
      destroyOnHidden
      footer={
        <div style={FOOTER}>
          <Space wrap>
            {draft && (
              <>
                <LookbackDaysInput value={lookback} disabled={busy} onChange={setDays} />
                <Button icon={<ReloadOutlined />} disabled={busy} onClick={handleRegenerate}>
                  {AI_LABELS.regenerate}
                </Button>
              </>
            )}
          </Space>
          <Space wrap>
            <Button onClick={onClose}>{MESSAGES.common.close}</Button>
            <Button
              type="primary"
              icon={<SendOutlined />}
              disabled={!draft || counts.sent === 0 || busy}
              loading={confirm.isPending}
              onClick={handleConfirm}
            >
              {fill(AI_LABELS.confirm, counts.sent)}
            </Button>
          </Space>
        </div>
      }
    >
      <QueryError error={query.error} onRetry={() => void query.refetch()} />
      {renderBody()}
    </Modal>
  );
}
