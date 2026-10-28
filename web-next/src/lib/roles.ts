// The API sends role as a plain string, so every comparison lives here and is
// covered by roles.test.ts. A typo can't silently hide the Treasurer section.
export function isTreasurer(role: string): boolean {
  return role === 'Treasurer';
}

export function roleText(role: string): string {
  switch (role) {
    case 'Treasurer':
      return "You're the Treasurer";
    case 'Admin':
      return "You're an Admin";
    case 'Member':
      return "You're a Member";
    default:
      return `Your role: ${role}`;
  }
}