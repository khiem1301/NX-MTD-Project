import { useState } from 'react';
import { Alert, Space, Tabs, Typography } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { AppDateRangeFields } from '@/components/common/DateInputs';
import { OrderSelect } from '@/components/common/OrderSelect';
import { API_DATE, toApiDate } from '@/utils/format';
import { LABELS, WAREHOUSE_TYPE, WAREHOUSE_TYPE_LABEL } from '../constants';
import { NxtReportTable } from './NxtReportTable';

type Period = { fromDate: string; toDate: string };

const defaultPeriod = (): Period => {
  const today = dayjs();
  return { fromDate: toApiDate(today.startOf('month')), toDate: toApiDate(today) };
};

/** Tab "Báo cáo nhập xuất tồn" của màn Tồn kho: tổng hợp theo đơn hàng và kỳ, mỗi kho một tab con. */
export function NxtReportPanel() {
  const [orderId, setOrderId] = useState<number>();
  const [period, setPeriod] = useState<Period>(defaultPeriod);

  return (
    <>
      <Typography.Paragraph type="secondary">{LABELS.nxtReportSubtitle}</Typography.Paragraph>
      <Space wrap style={{ marginBottom: 16 }}>
        <OrderSelect
          allowClear
          placeholder={LABELS.orderFilter}
          value={orderId}
          onChange={setOrderId}
          width={280}
        />
        <AppDateRangeFields
          allowClear={false}
          value={[dayjs(period.fromDate, API_DATE), dayjs(period.toDate, API_DATE)]}
          onChange={(range: [Dayjs | null, Dayjs | null] | null) => {
            if (!range?.[0] || !range[1]) return;
            setPeriod({ fromDate: toApiDate(range[0]), toDate: toApiDate(range[1]) });
          }}
        />
      </Space>

      {!orderId ? (
        <Alert type="info" showIcon message={LABELS.nxtSelectOrder} />
      ) : (
        <Tabs
          destroyOnHidden
          items={Object.values(WAREHOUSE_TYPE).map((type) => ({
            key: type,
            label: WAREHOUSE_TYPE_LABEL[type],
            children: (
              <NxtReportTable
                warehouseType={type}
                orderId={orderId}
                fromDate={period.fromDate}
                toDate={period.toDate}
              />
            ),
          }))}
        />
      )}
    </>
  );
}
