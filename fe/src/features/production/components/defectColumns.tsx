import { CloseCircleOutlined, EditOutlined, SwapOutlined } from '@ant-design/icons';
import { Space, Tag, Tooltip, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { IconAction } from '@/components/common/IconAction';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { formatDate } from '@/utils/format';
import { PRODUCTION_TEXT } from '../constants';
import { canModifyDefect, defectStateOf } from '../utils/defectRules';
import type { Defect } from '../types';

const text = PRODUCTION_TEXT.defects;

type DefectColumnsOptions = {
  isManager: boolean;
  onReplace: (defect: Defect) => void;
  onCancel: (defect: Defect) => void;
  onEdit: (defect: Defect) => void;
};

const reasonText = (d: Defect) =>
  d.reasonNote
    ? `${text.reason[d.defectReasonCode]}: ${d.reasonNote}`
    : text.reason[d.defectReasonCode];

/** Hai dòng chồng nhau: dòng đầu đậm, dòng sau mờ. Gộp nhiều thông tin vào một cột để bảng không phải cuộn ngang. */
const stacked = (top: string, bottom: string) => (
  <Space direction="vertical" size={0}>
    <Typography.Text strong>{top}</Typography.Text>
    <Typography.Text type="secondary">{bottom}</Typography.Text>
  </Space>
);

/**
 * Cột bảng hàng hỏng, gộp lại còn 7 cột: phiếu (mã, ngày, tổ), đơn / mã hàng, công đoạn / công nhân, loại và số lượng,
 * lý do, tình trạng, thao tác. Thông tin phụ nằm ở dòng mở rộng (DefectExpandedRow). Sửa (bút) và hủy (dấu x) hiện khi còn hạn 24 giờ của tổ trưởng,
 * hoặc với Quản đốc (kiểm ràng buộc ở BE).
 */
export const defectColumns = ({
  isManager,
  onReplace,
  onCancel,
  onEdit,
}: DefectColumnsOptions): ColumnsType<Defect> => [
  {
    title: text.columns.ticket,
    key: 'ticket',
    render: (_, d) =>
      stacked(
        d.defectCode,
        isManager ? `${formatDate(d.workDate)} · ${d.sewingTeamName}` : formatDate(d.workDate),
      ),
  },
  {
    responsive: ['md'],
    title: text.columns.orderProduct,
    key: 'order',
    render: (_, d) => stacked(d.orderCode, d.productCode),
  },
  {
    title: text.columns.operationWorker,
    key: 'op',
    render: (_, d) => stacked(`${d.operationCode} - ${d.operationName}`, d.responsibleWorkerName),
  },
  {
    title: text.columns.defect,
    key: 'defect',
    render: (_, d) => (
      <Space size={4}>
        <StatusTag status={d.defectType} labels={text.type} />
        <strong>{`×${d.quantity}`}</strong>
      </Space>
    ),
  },
  {
    responsive: ['lg'],
    title: text.columns.reason,
    key: 'reason',
    width: 200,
    ellipsis: { showTitle: false },
    render: (_, d) => (
      <Tooltip title={reasonText(d)}>
        <span>{reasonText(d)}</span>
      </Tooltip>
    ),
  },
  {
    title: text.columns.state,
    key: 'state',
    render: (_, d) => (
      <Space size={4} wrap>
        <StatusTag status={defectStateOf(d)} labels={text.state} />
        {d.replacementRequestCode && <Tag color="geekblue">{d.replacementRequestCode}</Tag>}
      </Space>
    ),
  },
  {
    title: MESSAGES.common.actions,
    key: 'action',
    fixed: 'right',
    render: (_, d) => (
      <Space size={0}>
        {canModifyDefect(d) && (
          <IconAction title={text.edit} icon={<EditOutlined />} onClick={() => onEdit(d)} />
        )}
        {!isManager &&
          d.defectType === 'Scrap' &&
          d.status === 'Recorded' &&
          !d.replacementRequestCode && (
            <IconAction
              title={PRODUCTION_TEXT.replacement.button}
              icon={<SwapOutlined />}
              onClick={() => onReplace(d)}
            />
          )}
        {canModifyDefect(d) && (
          <IconAction
            title={text.cancel}
            icon={<CloseCircleOutlined />}
            danger
            onClick={() => onCancel(d)}
          />
        )}
      </Space>
    ),
  },
];
