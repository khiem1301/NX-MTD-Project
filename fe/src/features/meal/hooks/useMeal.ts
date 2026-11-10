import { useQuery } from '@tanstack/react-query';
import { MESSAGES } from '@/constants/messages';
import { useApiMutation } from '@/hooks/useApiMutation';
import { mealApi } from '../api/mealApi';
import type {
  ConfirmTeamMealPayload,
  MealSettings,
  SetGuestMealPayload,
  SetMealPricePayload,
  SetMyMealPayload,
  SetTeamMemberMealPayload,
} from '../types';

export const MEAL_KEY = ['meal'] as const;
const suggestionKey = (date: string) => [...MEAL_KEY, 'suggestion', date] as const;
const membersKey = (date: string) => [...MEAL_KEY, 'members', date] as const;
const myMealKey = (date: string) => [...MEAL_KEY, 'my', date] as const;
const summaryKey = (date: string) => [...MEAL_KEY, 'summary', date] as const;

/** Admin: giờ nhắc và giờ chốt suất ăn. */
export const useMealSettings = () =>
  useQuery({ queryKey: [...MEAL_KEY, 'settings'], queryFn: mealApi.getSettings });

export const useSaveMealSettings = () =>
  useApiMutation((body: MealSettings) => mealApi.saveSettings(body), {
    invalidate: [MEAL_KEY],
    successMessage: MESSAGES.meal.settingsSaved,
  });

/** UC30 — tổ trưởng: gợi ý + checklist suất ăn của tổ trong ngày. */
export const useTeamMealSuggestion = (date: string) =>
  useQuery({
    queryKey: suggestionKey(date),
    queryFn: () => mealApi.getTeamSuggestion(date),
  });

export const useTeamMealMembers = (date: string) =>
  useQuery({
    queryKey: membersKey(date),
    queryFn: () => mealApi.getTeamMembers(date),
  });

export const useConfirmTeamMeal = (date: string) =>
  useApiMutation((body: ConfirmTeamMealPayload) => mealApi.confirmTeamMeal(body), {
    invalidate: [suggestionKey(date), membersKey(date), summaryKey(date)],
    successMessage: MESSAGES.meal.confirmSaved,
  });

export const useSetTeamMemberMeal = (date: string) =>
  useApiMutation((body: SetTeamMemberMealPayload) => mealApi.setTeamMemberMeal(body), {
    invalidate: [suggestionKey(date), membersKey(date), summaryKey(date)],
    successMessage: MESSAGES.meal.memberUpdated,
  });

/** Suất ăn của tôi. */
export const useMyMeal = (date: string) =>
  useQuery({
    queryKey: myMealKey(date),
    queryFn: () => mealApi.getMyMeal(date),
  });

export const useSetMyMeal = (date: string) =>
  useApiMutation((body: SetMyMealPayload) => mealApi.setMyMeal(body), {
    invalidate: [myMealKey(date), membersKey(date), summaryKey(date)],
    successMessage: MESSAGES.meal.memberUpdated,
  });

/** UC30 — Kế toán: tổng hợp suất ăn một ngày. */
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
