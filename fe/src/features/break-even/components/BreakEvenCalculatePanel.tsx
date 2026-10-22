import { useState } from 'react';
import { Button, Card, Modal, Select, Space } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { ALLOCATION_BASIS_OPTIONS, BREAK_EVEN_TEXT } from '../constants';
import { useCalculateBreakEven } from '../hooks/useBreakEven';
import type { AllocationBasis } from '../types';

type Props = { periodMonth: string };

/**
 * UC11 — Admin chọn cách phân bổ định phí (OUTPUT hoặc LABOUR_HOURS) rồi bấm "Tính toán". Mỗi lần tính
 * ghi thêm một bộ kết quả mới (lịch sử không bị mất) — vì không hoàn tác được nên có modal xác nhận.
 */
export function BreakEvenCalculatePanel({ periodMonth }: Props) {
  const [allocationBasis, setAllocationBasis] = useState<AllocationBasis>('Output');
  const calculate = useCalculateBreakEven(periodMonth);

  const handleCalculate = () => {
    Modal.confirm({
      title: MESSAGES.breakEven.confirmCalculateTitle,
      content: MESSAGES.breakEven.confirmCalculateContent,
      okText: MESSAGES.common.confirm,
      cancelText: MESSAGES.common.cancel,
      onOk: () => calculate.mutate(allocationBasis),
    });
  };

  return (
    <Card size="small" title={BREAK_EVEN_TEXT.calculate}>
      <Space wrap>
        <Select<AllocationBasis>
          style={{ width: 260 }}
          value={allocationBasis}
          options={ALLOCATION_BASIS_OPTIONS}
          onChange={setAllocationBasis}
        />
        <Button type="primary" loading={calculate.isPending} onClick={handleCalculate}>
          {BREAK_EVEN_TEXT.calculate}
        </Button>
      </Space>
    </Card>
  );
}
