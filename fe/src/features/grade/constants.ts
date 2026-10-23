import type { StatusLabel } from '@/components/common/StatusTag';

export const GRADE_OPTIONS = ['A', 'B', 'C', 'D'].map((value) => ({
  value,
  label: `Bậc ${value}`,
}));

export const EVAL_STATUS = {
  SUBMITTED: 'SUBMITTED',
  APPLIED: 'APPLIED',
  NOT_APPLIED: 'NOT_APPLIED',
} as const;

export const EVAL_STATUS_LABEL: Record<string, StatusLabel> = {
  SUBMITTED: { text: 'Chờ quyết định', color: 'gold' },
  APPLIED: { text: 'Đã áp dụng', color: 'green' },
  NOT_APPLIED: { text: 'Không áp dụng', color: 'default' },
};

export const GR_LABELS = {
  title: 'Bậc thợ',
  evaluationsTab: 'Đề xuất bậc',
  levelsTab: 'Mức tiền bậc',
  propose: 'Đề xuất bậc',
  worker: 'Công nhân',
  selectWorker: 'Chọn công nhân',
  currentGrade: 'Bậc hiện tại',
  proposedGrade: 'Bậc đề xuất',
  reason: 'Lý do đề xuất',
  defects: 'Số lỗi 30 ngày',
  team: 'Tổ',
  proposedBy: 'Người đề xuất',
  date: 'Ngày đề xuất',
  status: 'Trạng thái',
  decisionNote: 'Ghi chú quyết định',
  effectiveDate: 'Hiệu lực từ',
  apply: 'Áp dụng',
  notApply: 'Không áp dụng',
  notApplyTitle: 'Không áp dụng đề xuất bậc',
  applyTitle: 'Áp dụng bậc thợ',
  pending: 'Đang chờ đề xuất',
  addLevel: 'Thêm mức tiền bậc',
  gradeCode: 'Bậc',
  gradeName: 'Tên bậc',
  amount: 'Tiền bậc / tháng',
  current: 'Đang áp dụng',
  none: 'Chưa có',
} as const;

export const GR_MESSAGES = {
  proposed: 'Đã gửi đề xuất bậc thợ.',
  applied: 'Đã áp dụng bậc thợ.',
  notApplied: 'Đã ghi nhận không áp dụng bậc thợ.',
  levelSaved: 'Đã lưu mức tiền bậc.',
  levelHint: 'Đổi tiền bậc = thêm một dòng mới với ngày hiệu lực mới, các dòng cũ giữ làm lịch sử.',
} as const;
