import { useState } from 'react';
import { Alert, DatePicker, Form, Input, InputNumber, Modal, Select, Space, Table } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { OrderSelect } from '@/components/common/OrderSelect';
import { MESSAGES } from '@/constants/messages';
import { MAX_PAGE_SIZE } from '@/constants/pagination';
import { WAREHOUSE_TYPE, WAREHOUSE_TYPE_LABEL } from '@/features/warehouse/constants';
import { useBalances } from '@/features/warehouse/hooks/useInventory';
import type { StockBalance } from '@/features/warehouse/types';
import { formatQty } from '@/utils/format';
import { ST_LABELS } from '../constants';
import { useSaveStocktake } from '../hooks/useStocktake';
import type { Stocktake } from '../types';

type StocktakeFormModalProps = { open: boolean; stocktake: Stocktake | null; onClose: () => void };
type FormValues = {
  warehouseType: string;
  scope?: string;
  stocktakeDate: Dayjs;
  counted: Record<string, number | null>;
  notes: Record<string, string | undefined>;
};

const API_DATE = 'YYYY-MM-DD';
const WAREHOUSE_OPTIONS = Object.values(WAREHOUSE_TYPE).map((value) => ({
  value,
  label: WAREHOUSE_TYPE_LABEL[value],
}));

const rowKey = (b: StockBalance) =>
  b.materialId !== null
    ? `${b.materialId}|${b.materialColor ?? ''}`
    : `${b.orderLineId}:${b.productComponentMaterialId ?? ''}`;
const rowName = (b: StockBalance) =>
  b.materialId !== null
    ? `${b.materialCode} ${b.materialName}${b.materialColor ? ` - ${b.materialColor}` : ''}`
    : `${b.orderCode} · ${b.productCode} ${b.color}-${b.sizeCode}${b.componentName ? ` · ${b.componentName}` : ''}`;

/**
 * UC48: Thủ kho lập phiếu kiểm kê. Chọn kho (và đơn hàng cho kho BTP / thành phẩm), nạp tồn hiện tại làm số sổ,
 * nhập số đếm ở các dòng lệch (để trống = khớp sổ). Phiếu bị trả lại được sửa và gửi lại.
 */
export function StocktakeFormModal({ open, stocktake, onClose }: StocktakeFormModalProps) {
  const [form] = Form.useForm<FormValues>();
  const save = useSaveStocktake();
  const [warehouseType, setWarehouseType] = useState<string>(
    stocktake?.warehouseType ?? WAREHOUSE_TYPE.MATERIAL,
  );
  const [orderId, setOrderId] = useState<number>();
  const balances = useBalances({ warehouseType, orderId, page: 1, pageSize: MAX_PAGE_SIZE });
  const isMaterial = warehouseType === WAREHOUSE_TYPE.MATERIAL;
  const rows = (balances.data?.items ?? []).filter((b) => b.onHandQty > 0 || stocktake !== null);

  const initialCounted = Object.fromEntries(
    (stocktake?.lines ?? []).map((l) => [
      l.materialId !== null ? `${l.materialId}|${l.materialColor ?? ''}` : `${l.orderLineId}:${l.productComponentMaterialId ?? ''}`,
      l.countedQty,
    ]),
  );
  const initialNotes = Object.fromEntries(
    (stocktake?.lines ?? []).map((l) => [
      l.materialId !== null ? `${l.materialId}|${l.materialColor ?? ''}` : `${l.orderLineId}:${l.productComponentMaterialId ?? ''}`,
      l.note ?? undefined,
    ]),
  );

  const handleFinish = (values: FormValues) => {
    const lines = rows
      .map((b) => ({
        b,
        counted: values.counted?.[rowKey(b)] ?? null,
        note: values.notes?.[rowKey(b)]?.trim() || undefined,
      }))
      // Chỉ gửi dòng có nhập số đếm hoặc ghi chú: các dòng còn lại coi như khớp sổ.
      .filter((x) => x.counted !== null || x.note !== undefined)
      .map((x) => ({
        materialId: x.b.materialId ?? undefined,
        materialColor: x.b.materialId !== null ? (x.b.materialColor ?? '') : undefined,
        orderLineId: x.b.orderLineId ?? undefined,
        productComponentMaterialId: x.b.productComponentMaterialId,
        countedQty: x.counted,
        note: x.note,
      }));
    save.mutate(
      {
        id: stocktake?.stocktakeId,
        body: {
          warehouseType: values.warehouseType,
          scope: values.scope?.trim() || undefined,
          stocktakeDate: values.stocktakeDate.format(API_DATE),
          lines,
        },
      },
      { onSuccess: onClose },
    );
  };

  return (
    <Modal
      open={open}
      width={940}
      title={stocktake ? ST_LABELS.edit : ST_LABELS.create}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={save.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Form
        form={form}
        layout="vertical"
        disabled={save.isPending}
        onFinish={handleFinish}
        initialValues={{
          warehouseType,
          scope: stocktake?.scope ?? undefined,
          stocktakeDate: stocktake ? dayjs(stocktake.stocktakeDate) : dayjs(),
          counted: initialCounted,
          notes: initialNotes,
        }}
      >
        <Space wrap align="start">
          <Form.Item
            name="warehouseType"
            label={ST_LABELS.warehouse}
            rules={[{ required: true, message: MESSAGES.common.required }]}
          >
            <Select
              style={{ width: 200 }}
              options={WAREHOUSE_OPTIONS}
              disabled={stocktake !== null}
              onChange={(value: string) => {
                setWarehouseType(value);
                setOrderId(undefined);
                form.setFieldsValue({ counted: {}, notes: {} });
              }}
            />
          </Form.Item>
          {!isMaterial && (
            <Form.Item label={ST_LABELS.orderFilter}>
              <OrderSelect value={orderId} allowClear onChange={setOrderId} />
            </Form.Item>
          )}
          <Form.Item
            name="stocktakeDate"
            label={ST_LABELS.date}
            rules={[{ required: true, message: MESSAGES.common.required }]}
          >
            <DatePicker format="DD/MM/YYYY" disabledDate={(d) => d.isAfter(dayjs(), 'day')} />
          </Form.Item>
          <Form.Item name="scope" label={ST_LABELS.scope}>
            <Input placeholder={ST_LABELS.scopeHint} maxLength={300} style={{ width: 280 }} />
          </Form.Item>
        </Space>
        <Alert type="info" showIcon message={ST_LABELS.blankHint} style={{ marginBottom: 12 }} />
        <Table<StockBalance>
          rowKey={rowKey}
          size="small"
          loading={balances.isFetching}
          dataSource={rows}
          pagination={false}
          scroll={{ y: 360 }}
          locale={{ emptyText: ST_LABELS.noRows }}
          columns={[
            { title: ST_LABELS.item, key: 'item', render: (_, b) => rowName(b) },
            { title: ST_LABELS.unit, dataIndex: 'unit', render: (v: string | null) => v ?? 'cái' },
            { title: ST_LABELS.bookQty, dataIndex: 'onHandQty', align: 'right', render: formatQty },
            {
              title: ST_LABELS.countedQty,
              key: 'counted',
              render: (_, b) => (
                <Form.Item name={['counted', rowKey(b)]} noStyle>
                  <InputNumber min={0} />
                </Form.Item>
              ),
            },
            {
              title: ST_LABELS.note,
              key: 'note',
              render: (_, b) => (
                <Form.Item name={['notes', rowKey(b)]} noStyle>
                  <Input maxLength={300} />
                </Form.Item>
              ),
            },
          ]}
        />
      </Form>
    </Modal>
  );
}
