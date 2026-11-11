import { Form, Typography } from 'antd';
import { DataTable } from '@/components/common/DataTable';
import { QtyInput } from '@/components/common/QtyInput';
import { MESSAGES } from '@/constants/messages';
import type { FabricRequirement } from '@/types/workOrder';
import { fill, formatQty } from '@/utils/format';
import { LABELS, LAY_FABRIC_DECIMALS, LAY_MAX_FABRIC_USED_M } from '../constants';
import type { CuttingLay } from '../types';
import { teamFabricBalance } from '../utils/layRemaining';

type LayFabricBalanceProps = {
  /** Vải bàn phải nhập (theo các dòng có số lượng). */
  fabrics: FabricRequirement[];
  lay: CuttingLay | null;
};

/**
 * Số mét đã dùng của từng vải bàn cần (DEV-ALL-93): cắt cả bộ thì đủ mọi vải của bộ. Mỗi vải hiện còn lại của tổ trên đơn và
 * sau bàn này; bắt buộc nhập, không vượt số còn lại.
 */
export function LayFabricBalance({ fabrics, lay }: LayFabricBalanceProps) {
  const form = Form.useFormInstance();
  const used =
    (Form.useWatch('fabricUsed', form) as Record<string, number | null> | undefined) ?? {};

  return (
    <DataTable<FabricRequirement>
      rowKey="materialId"
      size="small"
      pagination={false}
      style={{ marginBottom: 12 }}
      dataSource={fabrics}
      locale={{ emptyText: LABELS.layFabricsEmpty }}
      columns={[
        {
          title: LABELS.fabricMaterial,
          key: 'fabric',
          render: (_, f) =>
            `${f.materialCode} ${f.materialName}${f.materialColor ? ` (${f.materialColor})` : ''}`,
        },
        {
          title: LABELS.layFabricUsed,
          key: 'used',
          width: 170,
          render: (_, f) => {
            const left = teamFabricBalance(f, lay, 0).left;
            return (
              <Form.Item
                name={['fabricUsed', String(f.materialId)]}
                style={{ marginBottom: 0 }}
                rules={[
                  { required: true, message: MESSAGES.common.required },
                  {
                    validator: async (_rule, v?: number) => {
                      if ((v ?? 0) > left)
                        throw new Error(
                          fill(MESSAGES.cutting.layFabricOverRemaining, formatQty(left)),
                        );
                    },
                  },
                ]}
              >
                <QtyInput
                  min={0.01}
                  max={LAY_MAX_FABRIC_USED_M}
                  decimals={LAY_FABRIC_DECIMALS}
                  style={{ width: '100%' }}
                  aria-label={`${LABELS.layFabricUsed} ${f.materialCode}`}
                />
              </Form.Item>
            );
          },
        },
        {
          title: LABELS.teamFabricLeft,
          key: 'left',
          align: 'right',
          render: (_, f) => formatQty(teamFabricBalance(f, lay, 0).left),
        },
        {
          title: LABELS.teamFabricAfter,
          key: 'after',
          align: 'right',
          render: (_, f) => {
            const b = teamFabricBalance(f, lay, used[String(f.materialId)] ?? 0);
            return (
              <>
                <Typography.Text strong type={b.after < 0 ? 'danger' : undefined}>
                  {formatQty(b.after)}
                </Typography.Text>
                {b.unmeasuredLays > 0 && (
                  <div>
                    <Typography.Text type="warning" style={{ fontSize: 12 }}>
                      {fill(LABELS.teamFabricUnmeasured, b.unmeasuredLays)}
                    </Typography.Text>
                  </div>
                )}
              </>
            );
          },
        },
      ]}
    />
  );
}
