import type { SearchResponse } from '../types';

export async function searchBooks(query: string): Promise<SearchResponse> {
  const response = await fetch('/api/books/search', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ query }),
  });

  if (!response.ok) {
    const err = await response.json().catch(() => ({}));
    throw new Error((err as { error?: string }).error ?? `Request failed: ${response.status}`);
  }

  return response.json();
}
