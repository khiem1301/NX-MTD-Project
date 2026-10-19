import { create } from 'zustand';
import type { UserInfo } from '@/types/auth';

type AuthStatus = 'loading' | 'authenticated' | 'unauthenticated';

type AuthState = {
  status: AuthStatus;
  user: UserInfo | null;
  setSession: (user: UserInfo) => void;
  clear: () => void;
};

export const useAuthStore = create<AuthState>((set) => ({
  status: 'loading',
  user: null,
  setSession: (user) => set({ status: 'authenticated', user }),
  clear: () => set({ status: 'unauthenticated', user: null }),
}));
