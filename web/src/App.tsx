import { ContributionList } from '@/components/ContributionList';
import { StokvelCard } from '@/components/StokvelCard';
import { ThemeToggle } from '@/components/ThemeToggle';
import { contributions, cycles, home } from '@/data/sample';

function App() {
  return (
    <main className="mx-auto w-full max-w-3xl space-y-6 p-4">
      <header className="flex flex-wrap items-center justify-between gap-2">
        <h1 className="text-2xl font-bold">RondiTrack</h1>
        <ThemeToggle />
      </header>

      <section aria-labelledby="stokvels-heading" className="space-y-3">
        <h2 id="stokvels-heading" className="text-lg font-semibold">
          My stokvels
        </h2>
        <div className="grid gap-3 sm:grid-cols-2">
          {home.map((entry) => (
            <StokvelCard key={entry.stokvelId} entry={entry} />
          ))}
        </div>
      </section>

      <ContributionList cycles={cycles} contributions={contributions} />
    </main>
  );
}

export default App;
