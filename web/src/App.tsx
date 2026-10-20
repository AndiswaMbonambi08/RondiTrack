import './App.css';
import { StokvelCard } from './components/StokvelCard';
import { home } from './data/sample';

function App() {
  return (
    <main>
      <h1>RondiTrack</h1>

      <section>
        <h2>My stokvels</h2>
        <div className="row">
          {home.map((entry) => (
            <StokvelCard key={entry.stokvelId} entry={entry} />
          ))}
        </div>
      </section>
    </main>
  );
}

export default App;
