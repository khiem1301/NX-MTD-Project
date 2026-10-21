import { Alert, Descriptions } from 'antd';
import { ACCOUNTING_REPORT_TEXT } from '../constants';
import type { MealReport } from '../types';
import { formatCurrency } from '../utils';

type Props = { report: MealReport };

/** UC36 — suất ăn phục vụ trong tháng theo hệ thống, đối soát với hóa đơn bếp đã nhập ở chi phí tháng
 * (MonthlyCost, khoản MEALS) nếu Kế toán đã nhập. */
export function MealReportPanel({ report }: Props) {
  return (
    <>
      <Descriptions column={{ xs: 1, sm: 2, md: 3 }} bordered size="small" style={{ marginBottom: 16 }}>
        <Descriptions.Item label={ACCOUNTING_REPORT_TEXT.mealSummary.daysWithData}>
          {report.daysWithData}
        </Descriptions.Item>
        <Descriptions.Item label={ACCOUNTING_REPORT_TEXT.mealSummary.totalMealsServed}>
          {report.totalMealsServed}
        </Descriptions.Item>
        <Descriptions.Item label={ACCOUNTING_REPORT_TEXT.mealSummary.totalMealAmountSystem}>
          {formatCurrency(report.totalMealAmountSystem)}
        </Descriptions.Item>
        <Descriptions.Item label={ACCOUNTING_REPORT_TEXT.mealSummary.monthlyCostMealsAmount}>
          {report.monthlyCostMealsAmount === null
            ? ACCOUNTING_REPORT_TEXT.mealSummary.notImported
            : formatCurrency(report.monthlyCostMealsAmount)}
        </Descriptions.Item>
      </Descriptions>

      {report.monthlyCostMealsAmount !== null ? (
        <Alert
          type={report.hasDiscrepancy ? 'warning' : 'success'}
          showIcon
          message={
            report.hasDiscrepancy
              ? ACCOUNTING_REPORT_TEXT.mealSummary.discrepancy
              : ACCOUNTING_REPORT_TEXT.mealSummary.match
          }
        />
      ) : null}
    </>
  );
}
