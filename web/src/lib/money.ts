const zar = new Intl.NumberFormat('en-ZA', { style: 'currency', currency: 'ZAR' })
export const formatCents = (cents: number) => zar.format(cents / 100)

const day = new Intl.DateTimeFormat('en-ZA', { dateStyle: 'medium', timeZone: 'Africa/Johannesburg' })
export const formatDay = (iso: string) => day.format(new Date(iso))