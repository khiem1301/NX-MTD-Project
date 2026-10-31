import { API } from '@/constants/apiEndpoints';
import { apiClient } from '@/services/apiClient';
import type { ApiResponse } from '@/types/api';
import type {
  ConfirmTeamMealPayload,
  DailyMealSummary,
  MealSettings,
  SetGuestMealPayload,
  SetMealPricePayload,
  TeamMealSuggestion,
} from '../types';

const get = async <T>(url: string, params?: object): Promise<T> =>
  (await apiClient.get<ApiResponse<T>>(url, { params })).data.data;
const put = async <T>(url: string, body: unknown): Promise<T> =>
  (await apiClient.put<ApiResponse<T>>(url, body)).data.data;
const post = async <T>(url: string, body?: unknown, params?: object): Promise<T> =>
  (await apiClient.post<ApiResponse<T>>(url, body, { params })).data.data;

export const mealApi = {
  getTeamSuggestion: (date: string) => get<TeamMealSuggestion>(API.meal.teamSuggestion, { date }),
  confirmTeamMeal: (body: ConfirmTeamMealPayload) => post<void>(API.meal.teamConfirmation, body),

  getSettings: () => get<MealSettings>(API.meal.settings),
  saveSettings: (body: MealSettings) => put<MealSettings>(API.meal.settings, body),

  getSummary: (date: string) => get<DailyMealSummary>(API.meal.summary, { date }),
  setGuestMeal: (body: SetGuestMealPayload) => post<void>(API.meal.guestCount, body),
  markKitchenNotified: (date: string) =>
    post<void>(API.meal.kitchenNotification, undefined, { date }),
  setMealPrice: (body: SetMealPricePayload) => post<void>(API.meal.price, body),
};
