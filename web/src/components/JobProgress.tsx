import type { PageCounts } from '../api/crawlerApi';

export function JobProgress({ counts }: { counts: PageCounts }) {
  const finished = counts.done + counts.skipped + counts.failed;

  return (
    <div className="progress">
      <label>
        Progress: {finished} of {counts.total} pages
        <progress value={finished} max={Math.max(counts.total, 1)} />
      </label>
      <p className="muted">
        {counts.done} done · {counts.skipped} skipped · {counts.failed} failed · {counts.queued} queued
      </p>
    </div>
  );
}
