import { useMemo } from 'react';
import { Alert, Modal, message } from 'antd';
import { DataTable } from '@/components/common/DataTable';
import { HintTip } from '@/components/common/HintTip';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { QueryError } from '@/components/common/QueryError';
import { MESSAGES } from '@/constants/messages';
import { fill, formatQty } from '@/utils/format';
import { FG_LABELS, FG_MESSAGES } from '../constants';
import { useHandoverAll, useTeamHandoverable } from '../hooks/useFgHandover';
import type { FgTeamHandoverableLine } from '../types';

type Props = { open: boolean; onClose: () => void };

/**
 * DEV-ALL-183: QA/QC bàn giao toàn bộ thành phẩm đã đạt QC của tổ mình vào kho. Tổ lấy theo tài khoản (không chọn tổ / đơn);
 * mỗi đơn + mã hàng một phiếu.
 */
export function FgHandoverAllModal({ open, onClose }: Props) {
  const lines = useTeamHandoverable(open);
  const handoverAll = useHandoverAll();
  const data = useMemo(() => lines.data ?? [], [lines.data]);
  const summary = useMemo(
    () => ({
      slips: new Set(data.map((l) => `${l.orderId}-${l.productId}`)).size,
      qty: data.reduce((sum, l) => sum + l.remainingQty, 0),
    }),
    [data],
  );

  return (
    <Modal
      open={open}
      width={MODAL_WIDTH.md}
      title={FG_LABELS.create}
      okText={MESSAGES.common.confirm}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={handoverAll.isPending}
      okButtonProps={{ disabled: data.length === 0 }}
      onOk={() =>
        Modal.confirm({
          title: FG_MESSAGES.confirmCreate,
          onOk: () =>
            handoverAll.mutate(undefined, {
              onSuccess: (r) => {
                void message.success(
                  fill(FG_MESSAGES.createdAll, r.handoverCount, formatQty(r.totalQty)),
                );
                onClose();
              },
            }),
        })
      }
      onCancel={onClose}
      destroyOnHidden
    >
      <HintTip text={FG_LABELS.teamHint} />
      <QueryError error={lines.error} onRetry={() => void lines.refetch()} />
      {!lines.isLoading && !lines.error && data.length === 0 && (
        <Alert
          type="info"
          showIcon
          message={FG_LABELS.nothingToHand}
          style={{ marginBottom: 12 }}
        />
      )}
      {data.length > 0 && <p>{fill(FG_LABELS.summary, summary.slips, formatQty(summary.qty))}</p>}
      <DataTable<FgTeamHandoverableLine>
        rowKey="orderLineId"
        size="small"
        pagination={false}
        loading={lines.isFetching}
        dataSource={data}
        scroll={{ y: 360 }}
        columns={[
          { title: FG_LABELS.order, dataIndex: 'orderCode' },
          { title: FG_LABELS.product, dataIndex: 'productCode' },
          {
            title: FG_LABELS.orderLine,
            key: 'line',
            render: (_, l) => `${l.color} - ${l.sizeCode}`,
          },
          { title: FG_LABELS.qcQty, dataIndex: 'qcQty', align: 'right' },
          { title: FG_LABELS.handedQty, dataIndex: 'handedQty', align: 'right' },
          { title: FG_LABELS.handoverQty, dataIndex: 'remainingQty', align: 'right' },
        ]}
      />
    </Modal>
  );
}
