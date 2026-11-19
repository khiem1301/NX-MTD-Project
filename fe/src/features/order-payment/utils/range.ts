/**
 * Khoảng số tiền nhập được cho một lần thanh toán: tổng đã thanh toán sau lần này phải nằm giữa 0 và giá trị chốt
 * (giá trị chốt âm thì nằm giữa giá trị chốt và 0). `paidByOthers` là tổng các lần khác (không tính lần đang sửa).
 */
export const allowedRange = (finalAmount: number, paidByOthers: number) => ({
  min: Math.min(0, finalAmount) - paidByOthers,
  max: Math.max(0, finalAmount) - paidByOthers,
});
