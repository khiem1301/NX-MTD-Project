import { ROLES } from '@/constants/roles';
import type { MenuEntry } from '@/types/menu';
import { MEAL_ROUTES } from './routes';
import { MEAL_TEXT } from './constants';

export const mealMenu: MenuEntry[] = [
  {
    key: 'team-meal',
    label: MEAL_TEXT.teamPageTitle,
    path: MEAL_ROUTES.teamMeal,
    roles: [ROLES.TeamLeader, ROLES.TeamDeputy],
  },
  {
    key: 'meal-summary',
    label: MEAL_TEXT.summaryPageTitle,
    path: MEAL_ROUTES.summary,
    roles: [ROLES.Accountant],
  },
];
