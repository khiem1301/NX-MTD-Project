import { useState } from 'react';
import { Alert, Button, Descriptions, Popconfirm, Typography } from 'antd';
import { ReasonModal } from '@/components/common/ReasonModal';
import { StatusTag } from '@/components/common/StatusTag';
import { ROLES } from '@/constants/roles';
import { WAREHOUSE_TYPE_LABEL } from '@/features/warehouse/constants';
import { usePermission } from '@/hooks/usePermission';
import { formatDate, formatDateTime, formatQty } from '@/utils/format';
import { ST_LABELS, ST_MESSAGES, STOCKTAKE_STATUS, STOCKTAKE_STATUS_LABEL } from '../constants';
import { useApproveStocktake, useReturnStocktake, useStocktake } from '../hooks/useStocktake';
import type { Stocktake, StocktakeLine } from '../types';
import { DetailModal, DetailDescriptions, ModalActions } from '@/components/common/DetailModal';
import { DataTable } from '@/components/common/DataTable';

type StocktakeModalProps = {
  stocktakeId: number | undefined;
  onClose: () => void;
  onEdit: (stocktake: Stocktake) => void;
};

const lineName = (l: StocktakeLine) =>
  l.materialId !== null
    ? `${l.materialCode} ${l.materialName}${l.materialColor ? ` - ${l.materialColor}` : ''}`
    : `${l.orderCode} · ${l.productCode} ${l.color}-${l.sizeCode}${l.componentName ? ` · ${l.componentName}` : ''}`;

/** Chi tiết phiếu kiểm kê: Quản đốc / Admin duyệt (điều chỉnh kho) hoặc trả lại; Thủ kho sửa phiếu bị trả. */
export function StocktakeModal({ stocktakeId, onClose, onEdit }: StocktakeModalProps) {
  const { hasRole } = usePermission();
  const detail = useStocktake(stocktakeId);
  const approve = useApproveStocktake();
  const giveBack = useReturnStocktake();
  const [returning, setReturning] = useState(false);
  const s = detail.data;
  const canDecide =
    hasRole(ROLES.Manager, ROLES.Admin) && s?.status === STOCKTAKE_STATUS.PENDING_APPROVAL;
  const canEdit = hasRole(ROLES.WarehouseKeeper) && s?.status === STOCKTAKE_STATUS.RETURNED;

  return (
    <DetailModal
      open={stocktakeId !== undefined}
      onClose={onClose}
      title={s?.stocktakeCode}
      loading={detail.isLoading}
      error={detail.error}
    >
      {s && (
        <>
          <ModalActions>
            {canDecide && (
              <>
                <Button danger onClick={() => setReturning(true)}>
                  {ST_LABELS.return}
                </Button>
                <Popconfirm
                  title={ST_MESSAGES.confirmApprove}
                  onConfirm={() => approve.mutate(s.stocktakeId, { onSuccess: onClose })}
                >
                  <Button type="primary" loading={approve.isPending}>
                    {ST_LABELS.approve}
                  </Button>
                </Popconfirm>
              </>
            )}
            {canEdit && <Button onClick={() => onEdit(s)}>{ST_LABELS.edit}</Button>}
          </ModalActions>
          {s.returnReason && (
            <Alert
              type="warning"
              showIcon
              message={`${ST_LABELS.returnReason}: ${s.returnReason}`}
              style={{ marginBottom: 16 }}
            />
          )}
          <DetailDescriptions>
            <Descriptions.Item label={ST_LABELS.warehouse}>
              {WAREHOUSE_TYPE_LABEL[s.warehouseType]}
            </Descriptions.Item>
            <Descriptions.Item label={ST_LABELS.status}>
              <StatusTag status={s.status} labels={STOCKTAKE_STATUS_LABEL} />
            </Descriptions.Item>
            <Descriptions.Item label={ST_LABELS.date}>
              {formatDate(s.stocktakeDate)}
            </Descriptions.Item>
            <Descriptions.Item label={ST_LABELS.createdAt}>
              {formatDateTime(s.createdAt)}
            </Descriptions.Item>
            <Descriptions.Item label={ST_LABELS.scope} span={2}>
              {s.scope ?? '—'}
            </Descriptions.Item>
          </DetailDescriptions>
          <DataTable<StocktakeLine>
            rowKey="stocktakeLineId"
            size="small"
            style={{ marginTop: 16 }}
            pagination={false}
            dataSource={s.lines}
            columns={[
              { title: ST_LABELS.item, key: 'item', render: (_, l) => lineName(l) },
              {
                title: ST_LABELS.unit,
                dataIndex: 'unit',
                render: (v: string | null) => v ?? 'cái',
              },
              { title: ST_LABELS.bookQty, dataIndex: 'bookQty', align: 'right', render: formatQty },
              {
                title: ST_LABELS.countedQty,
                dataIndex: 'countedQty',
                align: 'right',
                render: (v: number | null) => (v === null ? '—' : formatQty(v)),
              },
              {
                title: ST_LABELS.differenceQty,
                dataIndex: 'differenceQty',
                align: 'right',
                render: (v: number) =>
                  v === 0 ? (
                    '0'
                  ) : (
                    <Typography.Text type={v < 0 ? 'danger' : 'success'}>
                      {v > 0 ? `+${formatQty(v)}` : formatQty(v)}
                    </Typography.Text>
                  ),
              },
              { title: ST_LABELS.note, dataIndex: 'note', render: (v: string | null) => v ?? '—' },
            ]}
          />
          <ReasonModal
            open={returning}
            title={ST_LABELS.returnTitle}
            loading={giveBack.isPending}
            onCancel={() => setReturning(false)}
            onSubmit={(reason) =>
              giveBack.mutate(
                { id: s.stocktakeId, reason },
                {
                  onSuccess: () => {
                    setReturning(false);
                    onClose();
                  },
                },
              )
            }
          />
        </>
      )}
    </DetailModal>
  );
}
