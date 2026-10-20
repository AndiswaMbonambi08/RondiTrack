import { describe, expect, it } from 'vitest';
import { isTreasurer, roleText } from './roles';

describe('isTreasurer', () => {
  it('is true for Treasurer', () => {
    expect(isTreasurer('Treasurer')).toBe(true);
  });
  it('is false for Member', () => {
    expect(isTreasurer('Member')).toBe(false);
  });
  it('is false for a misspelling', () => {
    expect(isTreasurer('Treasuer')).toBe(false);
  });
});

describe('roleText', () => {
  it('words each role as text', () => {
    expect(roleText('Treasurer')).toBe("You're the Treasurer");
    expect(roleText('Member')).toBe("You're a Member");
    expect(roleText('Admin')).toBe("You're an Admin");
  });
});