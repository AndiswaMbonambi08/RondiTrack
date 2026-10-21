import { useEffect, useState, type ReactNode } from 'react';
import { ThemeContext, THEME_KEY, type Theme } from '@/lib/theme-context';

function readStoredTheme(): Theme {
  try {
    const v = localStorage.getItem(THEME_KEY);
    if (v === 'light' || v === 'dark' || v === 'system') return v;
  } catch {
    // private browsing: fall through to system
  }
  return 'system';
}

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [theme, setThemeState] = useState<Theme>(readStoredTheme);

  useEffect(() => {
    const mql = window.matchMedia('(prefers-color-scheme: dark)');
    const apply = () => {
      const dark = theme === 'dark' || (theme === 'system' && mql.matches);
      document.documentElement.classList.toggle('dark', dark);
      document.documentElement.style.colorScheme = dark ? 'dark' : 'light';
    };
    apply();
    if (theme !== 'system') return;
    mql.addEventListener('change', apply); // System follows the OS live
    return () => mql.removeEventListener('change', apply);
  }, [theme]);

  const setTheme = (t: Theme) => {
    try {
      localStorage.setItem(THEME_KEY, t);
    } catch {
      // storage unavailable: still switch for this session
    }
    setThemeState(t);
  };

  return <ThemeContext.Provider value={{ theme, setTheme }}>{children}</ThemeContext.Provider>;
}
