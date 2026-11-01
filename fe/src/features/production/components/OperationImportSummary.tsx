import { Descriptions, Typography } from 'antd';
import { PRODUCTION_TEXT } from '../constants';
import type { OperationImportPreview } from '../types';

const text = PRODUCTION_TEXT.operations.import;

type OperationImportSummaryProps = { data: OperationImportPreview; ratesOn: boolean };

/** Tóm tắt kết quả xem trước file Excel: số công đoạn thêm / cập nhật / giữ nguyên / lỗi, và đơn giá nếu nhập kèm. */
export function OperationImportSummary({ data, ratesOn }: OperationImportSummaryProps) {
  return (
    <Descriptions column={4} size="small" bordered>
      <Descriptions.Item label={text.summary.created}>
        <Typography.Text type="success">{data.newCount}</Typography.Text>
      </Descriptions.Item>
      <Descriptions.Item label={text.summary.updated}>{data.updateCount}</Descriptions.Item>
      <Descriptions.Item label={text.summary.unchanged}>{data.unchangedCount}</Descriptions.Item>
      <Descriptions.Item label={text.summary.error}>
        <Typography.Text type={data.errorCount > 0 ? 'danger' : undefined}>
          {data.errorCount}
        </Typography.Text>
      </Descriptions.Item>
      {ratesOn && (
        <>
          <Descriptions.Item label={text.rateSummary.created}>
            <Typography.Text type="success">{data.rateNewCount}</Typography.Text>
          </Descriptions.Item>
          <Descriptions.Item label={text.rateSummary.updated}>
            {data.rateUpdateCount}
          </Descriptions.Item>
          <Descriptions.Item label={text.rateSummary.unchanged}>
            {data.rateUnchangedCount}
          </Descriptions.Item>
          <Descriptions.Item label={text.rateSummary.skipped}>
            {data.rateSkippedCount}
          </Descriptions.Item>
        </>
      )}
    </Descriptions>
  );
}
