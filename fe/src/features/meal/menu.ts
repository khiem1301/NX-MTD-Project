import { ROLES } from '@/constants/roles';
import type { MenuEntry } from '@/types/menu';
import { MEAL_ROUTES } from '@/constants/routes';
import { MEAL_TEXT } from './constants';

export const mealMenu: MenuEntry[] = [
  {
    key: 'team-meal',
    label: MEAL_TEXT.teamPageTitle,
    path: MEAL_ROUTES.teamMeal,
    roles: [ROLES.TeamLeader, ROLES.TeamDeputy],
  },
  {
    key: 'my-meal',
    label: MEAL_TEXT.myPageTitle,
    path: MEAL_ROUTES.myMeal,
    // Tổ trưởng / tổ phó tick suất của mình trên màn Suất ăn của tổ.
    roles: [
      ROLES.Manager,
      ROLES.Accountant,
      ROLES.WarehouseKeeper,
      ROLES.QaQc,
      ROLES.Electrician,
      ROLES.Worker,
    ],
  },
  {
    key: 'meal-summary',
    label: MEAL_TEXT.summaryPageTitle,
    path: MEAL_ROUTES.summary,
    roles: [ROLES.Accountant],
  },
  {
    key: 'meal-settings',
    label: MEAL_TEXT.settingsPageTitle,
    path: MEAL_ROUTES.settings,
    roles: [ROLES.Admin],
  },
];
