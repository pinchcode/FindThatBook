import type { QueryHypothesis } from '../types';

interface Props {
  hypothesis: QueryHypothesis;
}

export function ParsedQueryBadge({ hypothesis }: Props) {
  const hasSomething = hypothesis.title || hypothesis.author || hypothesis.keywords.length > 0;
  if (!hasSomething) return null;

  return (
    <div className="flex flex-wrap items-center justify-center gap-2 text-sm">
      <span className="text-slate-400">AI understood:</span>
      {hypothesis.title && (
        <Pill label="title" value={hypothesis.title} color="indigo" />
      )}
      {hypothesis.author && (
        <Pill label="author" value={hypothesis.author} color="emerald" />
      )}
      {hypothesis.keywords.map((kw) => (
        <Pill key={kw} label="keyword" value={kw} color="amber" />
      ))}
    </div>
  );
}

function Pill({ label, value, color }: { label: string; value: string; color: string }) {
  const styles: Record<string, string> = {
    indigo: 'bg-indigo-50 dark:bg-indigo-900/30 text-indigo-700 dark:text-indigo-300 border-indigo-200 dark:border-indigo-700',
    emerald: 'bg-emerald-50 dark:bg-emerald-900/30 text-emerald-700 dark:text-emerald-300 border-emerald-200 dark:border-emerald-700',
    amber: 'bg-amber-50 dark:bg-amber-900/30 text-amber-700 dark:text-amber-300 border-amber-200 dark:border-amber-700',
  };

  return (
    <span className={`inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full border text-xs font-medium ${styles[color]}`}>
      <span className="opacity-60">{label}:</span>
      <span>{value}</span>
    </span>
  );
}
