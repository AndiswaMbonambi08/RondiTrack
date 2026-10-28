import type { Metadata } from 'next'
import './globals.css'

export const metadata: Metadata = { title: 'RondiTrack' }

// Runs before first paint: saved choice wins, otherwise follow the operating system.
const themeScript = `(function(){try{var t=localStorage.getItem('theme');var d=t==='dark'||(t!=='light'&&window.matchMedia('(prefers-color-scheme: dark)').matches);document.documentElement.classList.toggle('dark',d)}catch(e){}})()`

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    // suppressHydrationWarning: the script above adds the "dark" class before React hydrates.
    <html lang="en" suppressHydrationWarning>
      <head>
        <script dangerouslySetInnerHTML={{ __html: themeScript }} />
      </head>
      <body className="min-h-screen bg-background text-foreground antialiased">{children}</body>
    </html>
  )
}