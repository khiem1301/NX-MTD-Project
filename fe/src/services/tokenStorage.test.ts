import { tokenStorage } from './tokenStorage';

describe('tokenStorage', () => {
  beforeEach(() => localStorage.clear());

  it('returns null when nothing is stored', () => {
    expect(tokenStorage.getAccessToken()).toBeNull();
    expect(tokenStorage.getRefreshToken()).toBeNull();
  });

  it('stores and reads both tokens', () => {
    tokenStorage.setTokens({ accessToken: 'a', refreshToken: 'r' });
    expect(tokenStorage.getAccessToken()).toBe('a');
    expect(tokenStorage.getRefreshToken()).toBe('r');
  });

  it('clears both tokens', () => {
    tokenStorage.setTokens({ accessToken: 'a', refreshToken: 'r' });
    tokenStorage.clear();
    expect(tokenStorage.getAccessToken()).toBeNull();
    expect(tokenStorage.getRefreshToken()).toBeNull();
  });
});
