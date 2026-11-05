import { useState } from 'react';
import { Alert, Button, Select, Space, Typography, message } from 'antd';
import { ConfirmAction } from '@/components/common/ConfirmAction';
import { fill } from '@/utils/format';
import { OPERATION_LIMITS, PRODUCTION_TEXT } from '../constants';
import { useSetOperationsGroup } from '../hooks/useOperations';
import { useSubmitPieceRates } from '../hooks/usePieceRates';
import type { OperationGroup } from '../types';

const text = PRODUCTION_TEXT.operations.bulk;

type OperationGroupBulkBarProps = {
  productId: number;
  operationIds: number[];
  groups: OperationGroup[];
  /** Đơn giá Nháp của các dòng đã chọn, gửi duyệt được một lần (chỉ Kế toán có). */
  draftRateIds: number[];
  onDone: () => void;
};

/** Hiện khi đã tích chọn công đoạn, dính ở cuối màn hình: xếp tất cả công đoạn đã chọn vào một nhóm hoặc bỏ nhóm. */
export function OperationGroupBulkBar({
  productId,
  operationIds,
  groups,
  draftRateIds,
  onDone,
}: OperationGroupBulkBarProps) {
  const [groupId, setGroupId] = useState<number>();
  const change = useSetOperationsGroup();
  const submitRates = useSubmitPieceRates();
  const tooMany = operationIds.length > OPERATION_LIMITS.maxGroupChange;

  const apply = (operationGroupId: number | null) =>
    change.mutate(
      { productId, operationIds, operationGroupId },
      {
        onSuccess: (changed) => {
          if (changed === 0) void message.info(text.none);
          else void message.success(fill(text.done, changed));
          setGroupId(undefined);
          onDone();
        },
      },
    );

  const sendRates = () =>
    submitRates.mutate(draftRateIds, {
      onSuccess: (sent) => {
        void message.success(fill(text.submitDone, sent));
        onDone();
      },
    });

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
          {fill(text.selected, operationIds.length)}
          <Select<number>
            showSearch
            optionFilterProp="label"
            style={{ width: 240 }}
            placeholder={text.group}
            aria-label={text.group}
            notFoundContent={text.noGroups}
            options={groups.map((g) => ({ value: g.operationGroupId, label: g.groupName }))}
            value={groupId}
            onChange={setGroupId}
          />
          <Button
            type="primary"
            disabled={groupId === undefined || tooMany}
            loading={change.isPending}
            onClick={() => apply(groupId ?? null)}
          >
            {text.apply}
          </Button>
          <Button disabled={tooMany || change.isPending} onClick={() => apply(null)}>
            {text.ungroup}
          </Button>
          {draftRateIds.length > 0 && (
            <ConfirmAction
              title={fill(text.submitConfirm, draftRateIds.length)}
              onConfirm={sendRates}
            >
              <Button loading={submitRates.isPending}>
                {fill(text.submitRates, draftRateIds.length)}
              </Button>
            </ConfirmAction>
          )}
          <Button disabled={change.isPending} onClick={onDone}>
            {text.clear}
          </Button>
          {tooMany && (
            <Typography.Text type="danger">
              {fill(text.tooMany, OPERATION_LIMITS.maxGroupChange)}
            </Typography.Text>
          )}
        </Space>
      }
    />
  );
}
