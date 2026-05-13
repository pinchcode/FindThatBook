import { useState, type FormEvent } from 'react';

interface Props {
  onSearch: (query: string) => void;
  isLoading: boolean;
}

const EXAMPLES = [
  'tolkien hobbit illustrated deluxe 1937',
  'dickens tale two cities',
  'mark huckleberry',
  'austen bennet',
];

export function SearchBox({ onSearch, isLoading }: Props) {
  const [query, setQuery] = useState('');

  function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (query.trim()) onSearch(query.trim());
  }

  return (
    <div className="w-full max-w-2xl mx-auto">
      <form onSubmit={handleSubmit} className="flex gap-2">
        <input
          type="text"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder="e.g. tolkien hobbit illustrated deluxe 1937"
          disabled={isLoading}
          className="flex-1 min-w-0 px-4 py-3 rounded-xl border border-slate-200 dark:border-slate-700
                     bg-white dark:bg-slate-800 text-slate-900 dark:text-slate-100
                     placeholder:text-slate-400 focus:outline-none focus:ring-2
                     focus:ring-indigo-500 focus:border-transparent text-base
                     disabled:opacity-50 shadow-sm"
        />
        <button
          type="submit"
          disabled={isLoading || !query.trim()}
          className="shrink-0 px-6 py-3 rounded-xl bg-indigo-600 hover:bg-indigo-700 active:bg-indigo-800
                     text-white font-medium text-base transition-colors disabled:opacity-50
                     disabled:cursor-not-allowed shadow-sm"
        >
          {isLoading ? (
            <span className="flex items-center gap-2">
              <Spinner />
              Searching…
            </span>
          ) : (
            'Search'
          )}
        </button>
      </form>

      <div className="mt-3 flex flex-wrap gap-2 justify-center">
        <span className="text-sm text-slate-400 self-center">Try:</span>
        {EXAMPLES.map((ex) => (
          <button
            key={ex}
            onClick={() => { setQuery(ex); onSearch(ex); }}
            disabled={isLoading}
            className="text-sm px-3 py-1 rounded-full border border-slate-200 dark:border-slate-700
                       text-slate-600 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-slate-800
                       transition-colors disabled:opacity-40"
          >
            {ex}
          </button>
        ))}
      </div>
    </div>
  );
}

function Spinner() {
  return (
    <svg className="animate-spin h-4 w-4" viewBox="0 0 24 24" fill="none">
      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
      <path className="opacity-75" fill="currentColor"
        d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
    </svg>
  );
}
