/** Nguồn số suất ăn của tổ: tổ trưởng tự xác nhận, hoặc hệ thống lấy theo chấm công (JOB-02/JOB-03). */
export type MealConfirmationSource = 'Confirmed' | 'Attendance';

/** Trạng thái suất ăn ngày. */
export type MealStatus = 'Open' | 'AutoClosed' | 'Closed' | 'KitchenNotified';

/** Gợi ý + tình trạng xác nhận suất ăn của tổ mình, cho màn tổ trưởng. */
export type TeamMealSuggestion = {
  mealDate: string;
  teamId: number;
  teamName: string;
  suggestedCount: number;
  mealCount: number | null;
  source: MealConfirmationSource | null;
  confirmedAt: string | null;
  isLocked: boolean;
};

/** Giờ nhắc xác nhận và giờ tự chốt suất ăn, dạng HH:mm. */
export type MealSettings = { reminderTime: string; cutoffTime: string };

export type ConfirmTeamMealPayload = {
  mealDate: string;
  mealCount: number;
};

export type SetGuestMealPayload = {
  mealDate: string;
  guestCount: number;
  guestNote?: string | null;
};

/** Một dòng tổ trong bảng tổng hợp; source = null nghĩa là tổ chưa xác nhận (số là gợi ý). */
export type TeamMealRow = {
  teamId: number;
  teamName: string;
  mealCount: number;
  attendanceCount: number;
  source: MealConfirmationSource | null;
  confirmedAt: string | null;
};

/** Tổng hợp suất ăn một ngày cho Kế toán. */
export type DailyMealSummary = {
  dailyMealId: number;
  mealDate: string;
  status: MealStatus;
  officeCount: number;
  guestCount: number;
  guestNote: string | null;
  teamTotal: number;
  totalMeals: number;
  mealPrice: number | null;
  suggestedMealPrice: number | null;
  totalAmount: number | null;
  attendanceIncomplete: boolean;
  autoClosedAt: string | null;
  closedAt: string | null;
  kitchenNotifiedAt: string | null;
  teams: TeamMealRow[];
};

export type SetMealPricePayload = {
  mealDate: string;
  mealPrice: number;
};
