import { Button, Card, Typography } from 'antd';
import { DownloadOutlined } from '@ant-design/icons';
import { PAYROLL_TEXT } from '../constants';
import { useExportPayroll } from '../hooks/usePayroll';

type Props = { periodMonth: string };

/** UC31 — Kế toán xuất dữ liệu tính lương tháng ra Excel (MSG27) để tính lương ngoài hệ thống. */
export function PayrollExportPanel({ periodMonth }: Props) {
  const exportPayroll = useExportPayroll();

  return (
    <Card size="small">
      <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
        {PAYROLL_TEXT.accountantPageSubtitle}
      </Typography.Paragraph>
      <Button
        icon={<DownloadOutlined />}
        loading={exportPayroll.isPending}
        onClick={() => exportPayroll.mutate(periodMonth)}
      >
        {PAYROLL_TEXT.export}
      </Button>
    </Card>
  );
}
