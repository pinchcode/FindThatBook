import { useState } from 'react';
import { searchBooks } from './api/books';
import { SearchBox } from './components/SearchBox';
import { BookCard } from './components/BookCard';
import { ParsedQueryBadge } from './components/ParsedQueryBadge';
import type { SearchResponse } from './types';

type State =
  | { status: 'idle' }
  | { status: 'loading' }
  | { status: 'success'; data: SearchResponse }
  | { status: 'error'; message: string };

export default function App() {
  const [state, setState] = useState<State>({ status: 'idle' });

  async function handleSearch(query: string) {
    setState({ status: 'loading' });
    try {
      const data = await searchBooks(query);
      setState({ status: 'success', data });
    } catch (err) {
      setState({ status: 'error', message: err instanceof Error ? err.message : 'Something went wrong.' });
    }
  }

  return (
    <div className="min-h-screen bg-slate-50 dark:bg-slate-900 text-slate-900 dark:text-slate-100">
      <div className="max-w-3xl mx-auto px-4 py-12">

        <header className="text-center mb-10">
          <h1 className="text-4xl font-bold tracking-tight text-slate-900 dark:text-slate-100">
            Find That Book
          </h1>
          <p className="mt-2 text-slate-500 dark:text-slate-400">
            Describe any book — messy, partial, or noisy — and we'll find it.
          </p>
        </header>

        <SearchBox onSearch={handleSearch} isLoading={state.status === 'loading'} />

        <div className="mt-10">
          {state.status === 'loading' && (
            <div className="text-center text-slate-400 py-16 animate-pulse">
              Thinking…
            </div>
          )}

          {state.status === 'error' && (
            <div className="rounded-xl border border-red-200 dark:border-red-800 bg-red-50
                            dark:bg-red-900/20 text-red-700 dark:text-red-300 px-5 py-4 text-sm">
              {state.message}
            </div>
          )}

          {state.status === 'success' && (
            <>
              {state.data.apiWarning && (
                <div className="mb-6 rounded-xl border border-amber-200 dark:border-amber-700
                                bg-amber-50 dark:bg-amber-900/20 text-amber-800 dark:text-amber-300
                                px-4 py-3 text-sm flex items-start gap-2">
                  <span className="mt-0.5">⚠️</span>
                  <span>{state.data.apiWarning}</span>
                </div>
              )}
              {state.data.parsedQuery && (
                <div className="mb-6">
                  <ParsedQueryBadge hypothesis={state.data.parsedQuery} />
                </div>
              )}

              {state.data.candidates.length === 0 ? (
                <div className="text-center text-slate-400 py-16">
                  No matches found. Try rephrasing your query.
                </div>
              ) : (
                <div className="space-y-4">
                  <p className="text-sm text-slate-400 text-center">
                    {state.data.candidates.length} candidate{state.data.candidates.length !== 1 ? 's' : ''} found
                  </p>
                  {state.data.candidates.map((candidate, i) => (
                    <BookCard key={candidate.workKey || i} candidate={candidate} rank={i + 1} />
                  ))}
                </div>
              )}
            </>
          )}
        </div>
      </div>
    </div>
  );
}
