import { toPageRoutes } from '@/components/guards/pageRoutes';
import { ROLES } from '@/constants/roles';
import { MEAL_ROUTES } from '@/constants/routes';

/** Suất ăn (Minh). */
export const mealRoutes = toPageRoutes([
  {
    path: MEAL_ROUTES.teamMeal,
    roles: [ROLES.TeamLeader, ROLES.TeamDeputy],
    page: async () => (await import('./pages/TeamMealPage')).TeamMealPage,
  },
  {
    path: MEAL_ROUTES.myMeal,
    roles: Object.values(ROLES).filter((r) => r !== ROLES.Admin),
    page: async () => (await import('./pages/MyMealPage')).MyMealPage,
  },
  {
    path: MEAL_ROUTES.summary,
    roles: [ROLES.Accountant],
    page: async () => (await import('./pages/MealSummaryPage')).MealSummaryPage,
  },
  {
    path: MEAL_ROUTES.settings,
    roles: [ROLES.Admin],
    page: async () => (await import('./pages/MealSettingsPage')).MealSettingsPage,
  },
]);
