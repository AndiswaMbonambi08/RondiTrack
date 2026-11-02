// The API sends role as a plain string, so every comparison lives here. A typo can't silently
// hide the Treasurer section.
export function isTreasurer(role: string): boolean {
  return role === 'Treasurer';
}

// The API lets a Treasurer or an Admin record a contribution for any member of THAT stokvel
// (and list its members). Pass the role from the API's answer for this stokvel, never one
// from the session or the token.
export function canRecordForOthers(role: string): boolean {
  return role === 'Treasurer' || role === 'Admin';
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