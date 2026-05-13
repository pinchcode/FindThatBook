import { useState } from 'react';
import type { BookCandidate } from '../types';

interface Props {
  candidate: BookCandidate;
  rank: number;
}

export function BookCard({ candidate, rank }: Props) {
  const { title, author, allAuthors, firstPublishYear, workUrl, coverImageUrl, explanation } = candidate;
  const contributors = allAuthors.filter(a => a !== author);
  const [expanded, setExpanded] = useState(false);

  return (
    <article className="flex gap-4 p-5 rounded-2xl border border-slate-200 dark:border-slate-700
                        bg-white dark:bg-slate-800 shadow-sm hover:shadow-md transition-shadow">
      {/* Rank badge */}
      <div className="flex-shrink-0 w-7 h-7 rounded-full bg-indigo-100 dark:bg-indigo-900/50
                      text-indigo-700 dark:text-indigo-300 text-sm font-semibold
                      flex items-center justify-center mt-0.5">
        {rank}
      </div>

      {/* Cover */}
      <div className="flex-shrink-0 w-16 h-24 rounded-lg overflow-hidden bg-slate-100 dark:bg-slate-700">
        {coverImageUrl ? (
          <img
            src={coverImageUrl}
            alt={`Cover of ${title}`}
            className="w-full h-full object-cover"
            loading="lazy"
          />
        ) : (
          <div className="w-full h-full flex items-center justify-center text-slate-300 dark:text-slate-500">
            <BookIcon />
          </div>
        )}
      </div>

      {/* Details */}
      <div className="flex-1 min-w-0 text-left">
        <a
          href={workUrl}
          target="_blank"
          rel="noopener noreferrer"
          className="font-semibold text-slate-900 dark:text-slate-100 hover:text-indigo-600
                     dark:hover:text-indigo-400 transition-colors line-clamp-2 leading-snug"
        >
          {title}
        </a>

        <div className="mt-1 flex flex-wrap items-center gap-x-2 gap-y-1 text-sm text-slate-500 dark:text-slate-400">
          {author && <span>{author}</span>}
          {author && firstPublishYear && <span className="text-slate-300 dark:text-slate-600">·</span>}
          {firstPublishYear && <span>{firstPublishYear}</span>}
        </div>
        {contributors.length > 0 && (
          <div className="mt-0.5 text-xs text-slate-400 dark:text-slate-500">
            <span className={expanded ? '' : 'line-clamp-2'}>
              Also: {contributors.join(', ')}
            </span>
            {contributors.join(', ').length > 80 && (
              <button
                onClick={() => setExpanded(e => !e)}
                className="ml-1 text-indigo-500 hover:underline focus:outline-none"
              >
                {expanded ? 'Show less' : 'Show more'}
              </button>
            )}
          </div>
        )}

        <p className="mt-2 text-sm text-slate-600 dark:text-slate-300 leading-relaxed">
          {explanation}
        </p>

        <a
          href={workUrl}
          target="_blank"
          rel="noopener noreferrer"
          className="mt-2 inline-flex items-center gap-1 text-xs text-indigo-600 dark:text-indigo-400
                     hover:underline"
        >
          View on Open Library
          <ExternalLinkIcon />
        </a>
      </div>
    </article>
  );
}

function BookIcon() {
  return (
    <svg className="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
      <path strokeLinecap="round" strokeLinejoin="round"
        d="M12 6.042A8.967 8.967 0 006 3.75c-1.052 0-2.062.18-3 .512v14.25A8.987 8.987 0 016 18c2.305 0 4.408.867 6 2.292m0-14.25a8.966 8.966 0 016-2.292c1.052 0 2.062.18 3 .512v14.25A8.987 8.987 0 0018 18a8.967 8.967 0 00-6 2.292m0-14.25v14.25" />
    </svg>
  );
}

function ExternalLinkIcon() {
  return (
    <svg className="w-3 h-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
      <path strokeLinecap="round" strokeLinejoin="round"
        d="M10 6H6a2 2 0 00-2 2v10a2 2 0 002 2h10a2 2 0 002-2v-4M14 4h6m0 0v6m0-6L10 14" />
    </svg>
  );
}
