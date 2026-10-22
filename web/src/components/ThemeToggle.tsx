import { useSyncExternalStore } from 'react';
import { Moon, Sun } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { useTheme } from '@/hooks/useTheme';

const query = '(prefers-color-scheme: dark)';
const subscribe = (cb: () => void) => {
  const mql = window.matchMedia(query);
  mql.addEventListener('change', cb);
  return () => mql.removeEventListener('change', cb);
};
const systemPrefersDark = () => window.matchMedia(query).matches;

export function ThemeToggle() {
  const { theme, setTheme } = useTheme();
  const osDark = useSyncExternalStore(subscribe, systemPrefersDark);
  const dark = theme === 'dark' || (theme === 'system' && osDark);
  const label = dark ? 'Switch to light mode' : 'Switch to dark mode';

  return (
    <Button
      type="button"
      size="sm"
      variant="outline"
      aria-label={label}
      title={label}
      onClick={() => setTheme(dark ? 'light' : 'dark')}
      className="min-h-9 min-w-9"
    >
      {dark ? <Sun aria-hidden="true" /> : <Moon aria-hidden="true" />}
    </Button>
  );
}