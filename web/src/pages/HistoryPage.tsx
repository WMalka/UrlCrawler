import type { ReactNode } from 'react';
import type { JobSummary } from '../api/crawlerApi';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { EmptyState } from '../components/EmptyState';
import { ErrorState } from '../components/ErrorState';
import { LoadingState } from '../components/LoadingState';
import { StatusBadge } from '../components/StatusBadge';
import { formatDateTime } from '../format';
import { useJobHistory } from '../hooks/useJobHistory';

const PAGE_SIZE = 20;

export function HistoryPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const page = Math.max(1, Math.floor(Number(searchParams.get('page'))) || 1);
  const { data, error, isPending, isPlaceholderData, refetch } = useJobHistory(page, PAGE_SIZE);

  let content: ReactNode;
  if (isPending) {
    content = <LoadingState label="Loading history…" />;
  } else if (error) {
    content = <ErrorState error={error} onRetry={refetch} />;
  } else if (data.totalCount === 0) {
    content = (
      <EmptyState>
        No crawls yet. <Link to="/">Start one</Link>.
      </EmptyState>
    );
  } else {
    const totalPages = Math.max(1, Math.ceil(data.totalCount / data.pageSize));
    const goToPage = (next: number) => setSearchParams({ page: String(next) });

    content = (
      <>
        {data.items.length > 0 ? (
          <JobTable items={data.items} busy={isPlaceholderData} />
        ) : (
          <EmptyState>
            No jobs on this page. <Link to="/history">Go to the first page</Link>.
          </EmptyState>
        )}
        <nav className="pager" aria-label="Pagination">
          <button type="button" onClick={() => goToPage(page - 1)} disabled={page <= 1 || isPlaceholderData}>
            Previous
          </button>
          <span>
            Page {page} of {totalPages} ({data.totalCount} jobs)
          </span>
          <button
            type="button"
            onClick={() => goToPage(page + 1)}
            disabled={page >= totalPages || isPlaceholderData}
          >
            Next
          </button>
        </nav>
      </>
    );
  }

  return (
    <section>
      <h1>Crawl history</h1>
      {content}
    </section>
  );
}

type JobTableProps = {
  items: JobSummary[];
  busy: boolean;
};

function JobTable({ items, busy }: JobTableProps) {
  const navigate = useNavigate();

  return (
    <table className="table" aria-busy={busy}>
      <thead>
        <tr>
          <th scope="col">URL</th>
          <th scope="col">Status</th>
          <th scope="col">Created</th>
          <th scope="col">Completed</th>
        </tr>
      </thead>
      <tbody>
        {items.map((job) => (
          <tr key={job.jobId} className="clickable" onClick={() => navigate(`/jobs/${job.jobId}`)}>
            <td>
              <Link to={`/jobs/${job.jobId}`}>{job.url}</Link>
            </td>
            <td>
              <StatusBadge status={job.status} />
            </td>
            <td>{formatDateTime(job.createdAt)}</td>
            <td>{formatDateTime(job.completedAt)}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
