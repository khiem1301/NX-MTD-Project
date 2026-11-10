import { useEffect, useState } from 'react';
import { Alert, Form, Input, Modal, Select, Space, message } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { OrderSelect } from '@/components/common/OrderSelect';
import { MESSAGES } from '@/constants/messages';
import { WAREHOUSE_TYPE, WAREHOUSE_TYPE_LABEL } from '@/constants/warehouse';
import { useBalances } from '@/hooks/useStockBalances';
import { useTableQuery } from '@/hooks/useTableQuery';
import { API_DATE } from '@/utils/format';
import { ST_LABELS, ST_MESSAGES } from '../constants';
import { useSaveStocktake } from '../hooks/useStocktake';
import type { Stocktake } from '../types';
import {
  MAX_LINES,
  targetKey,
  toStocktakeLines,
  type CountValues,
  type LineTarget,
} from '../utils/stocktakeLines';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { DebouncedSearch } from '@/components/common/DebouncedSearch';
import { AppDatePicker } from '@/components/common/DateInputs';
import { StocktakeCountTable } from './StocktakeCountTable';

type StocktakeFormModalProps = { open: boolean; stocktake: Stocktake | null; onClose: () => void };
type FormValues = CountValues & {
  warehouseType: string;
  scope?: string;
  stocktakeDate: Dayjs;
};
type Filters = { keyword?: string; orderId?: number };

const WAREHOUSE_OPTIONS = Object.values(WAREHOUSE_TYPE).map((value) => ({
  value,
  label: WAREHOUSE_TYPE_LABEL[value],
}));

/**
 * UC48: Thủ kho lập phiếu kiểm kê. Chọn kho (và đơn hàng cho kho BTP / thành phẩm), nạp tồn hiện tại làm số sổ,
 * nhập số đếm ở các dòng lệch (để trống = khớp sổ). Danh sách tồn phân trang phía máy chủ, số đã nhập giữ nguyên khi
 * chuyển trang. Phiếu bị trả lại được sửa và gửi lại.
 */
export function StocktakeFormModal({ open, stocktake, onClose }: StocktakeFormModalProps) {
  const [form] = Form.useForm<FormValues>();
  const save = useSaveStocktake();
  const [warehouseType, setWarehouseType] = useState<string>(
    stocktake?.warehouseType ?? WAREHOUSE_TYPE.MATERIAL,
  );
  const table = useTableQuery<Filters>({});
  const balances = useBalances({ ...table.params, warehouseType });
  const isMaterial = warehouseType === WAREHOUSE_TYPE.MATERIAL;
  // Mọi hàng hóa đã hiện ở bất kỳ trang nào (và dòng của phiếu đang sửa): nguồn để dựng danh sách dòng gửi đi.
  const [targets] = useState(
    () =>
      new Map<string, LineTarget>(
        (stocktake?.lines ?? []).map((l): [string, LineTarget] => [targetKey(l), l]),
      ),
  );
  useEffect(() => {
    (balances.data?.items ?? []).forEach((b) => targets.set(targetKey(b), b));
  }, [balances.data, targets]);

  const lineValues = <V,>(pick: (l: Stocktake['lines'][number]) => V) =>
    Object.fromEntries((stocktake?.lines ?? []).map((l) => [targetKey(l), pick(l)]));

  const handleFinish = (values: FormValues) => {
    const lines = toStocktakeLines(form.getFieldsValue(true) as CountValues, targets);
    if (lines.length === 0) return void message.warning(ST_MESSAGES.noLines);
    if (lines.length > MAX_LINES) return void message.warning(ST_MESSAGES.tooManyLines);
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

  const changeWarehouse = (value: string) => {
    setWarehouseType(value);
    targets.clear();
    table.setFilters({ keyword: undefined, orderId: undefined });
    form.setFieldsValue({ counted: {}, notes: {} });
  };

  return (
    <Modal
      open={open}
      width={MODAL_WIDTH.lg}
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
          counted: lineValues((l) => l.countedQty),
          notes: lineValues((l) => l.note ?? undefined),
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
              onChange={changeWarehouse}
            />
          </Form.Item>
          <Form.Item
            name="stocktakeDate"
            label={ST_LABELS.date}
            rules={[{ required: true, message: MESSAGES.common.required }]}
          >
            <AppDatePicker disabledDate={(d) => d.isAfter(dayjs(), 'day')} />
          </Form.Item>
          <Form.Item name="scope" label={ST_LABELS.scope}>
            <Input placeholder={ST_LABELS.scopeHint} maxLength={300} style={{ width: 280 }} />
          </Form.Item>
        </Space>
        <Space wrap style={{ marginBottom: 12 }}>
          <DebouncedSearch
            key={warehouseType}
            allowClear
            placeholder={MESSAGES.common.search}
            onSearch={(keyword) => table.setFilters({ keyword: keyword || undefined })}
            style={{ width: 240 }}
          />
          {!isMaterial && (
            <OrderSelect
              allowClear
              placeholder={ST_LABELS.orderFilter}
              value={table.filters.orderId}
              onChange={(orderId) => table.setFilters({ orderId })}
            />
          )}
        </Space>
        <Alert type="info" showIcon message={ST_LABELS.blankHint} style={{ marginBottom: 12 }} />
        <StocktakeCountTable
          isMaterial={isMaterial}
          balances={balances.data?.items}
          loading={balances.isFetching}
          pagination={table.pagination(balances.data?.totalItems)}
        />
      </Form>
    </Modal>
  );
}
