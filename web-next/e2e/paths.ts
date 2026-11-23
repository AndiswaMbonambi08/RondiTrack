import path from 'node:path'

// e2e/.auth is git-ignored. Never commit a saved session.
export const AUTH_DIR = path.join(__dirname, '.auth')
export const SIPHO_STATE = path.join(AUTH_DIR, 'sipho.json')
export const API_TOKENS = path.join(AUTH_DIR, 'api.json')