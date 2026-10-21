import { useQuery } from '@tanstack/react-query';
import { MESSAGES } from '@/constants/messages';
import { useApiMutation } from '@/hooks/useApiMutation';
import { mealApi } from '../api/mealApi';
import type { ConfirmTeamMealPayload, SetGuestMealPayload, SetMealPricePayload } from '../types';

export const MEAL_KEY = ['meal'] as const;
const suggestionKey = (date: string) => [...MEAL_KEY, 'suggestion', date] as const;
const summaryKey = (date: string) => [...MEAL_KEY, 'summary', date] as const;

/** UC30 — tổ trưởng: gợi ý + tình trạng xác nhận suất ăn của tổ mình trong ngày. */
export const useTeamMealSuggestion = (date: string) =>
  useQuery({
    queryKey: suggestionKey(date),
    queryFn: () => mealApi.getTeamSuggestion(date),
  });

export const useConfirmTeamMeal = (date: string) =>
  useApiMutation((body: ConfirmTeamMealPayload) => mealApi.confirmTeamMeal(body), {
    invalidate: [suggestionKey(date), summaryKey(date)],
    successMessage: MESSAGES.meal.confirmSaved,
  });

/** UC30 — Kế toán: tổng hợp suất ăn một ngày (tổ, văn phòng, khách, tổng, đơn giá). */
export const useMealSummary = (date: string) =>
  useQuery({
    queryKey: summaryKey(date),
    queryFn: () => mealApi.getSummary(date),
  });

export const useSetGuestMeal = (date: string) =>
  useApiMutation((body: SetGuestMealPayload) => mealApi.setGuestMeal(body), {
    invalidate: [summaryKey(date)],
    successMessage: MESSAGES.meal.guestSaved,
  });

export const useMarkKitchenNotified = (date: string) =>
  useApiMutation(() => mealApi.markKitchenNotified(date), {
    invalidate: [summaryKey(date)],
    successMessage: MESSAGES.meal.kitchenNotified,
  });

export const useSetMealPrice = (date: string) =>
  useApiMutation((body: SetMealPricePayload) => mealApi.setMealPrice(body), {
    invalidate: [summaryKey(date)],
    successMessage: MESSAGES.meal.priceSaved,
  });
