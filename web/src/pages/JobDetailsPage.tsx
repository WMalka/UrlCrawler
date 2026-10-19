import { useParams } from 'react-router-dom';
import { isActiveStatus, type Job } from '../api/crawlerApi';
import { EmptyState } from '../components/EmptyState';
import { ErrorState } from '../components/ErrorState';
import { JobProgress } from '../components/JobProgress';
import { LoadingState } from '../components/LoadingState';
import { PageTree } from '../components/PageTree';
import { StatusBadge } from '../components/StatusBadge';
import { formatDateTime } from '../format';
import { useCancelJob } from '../hooks/useCancelJob';
import { useJob } from '../hooks/useJob';
import { useJobTree } from '../hooks/useJobTree';

export function JobDetailsPage() {
  const { jobId = '' } = useParams();
  const { data: job, error, isPending, refetch } = useJob(jobId);

  return (
    <section>
      <h1>Job details</h1>
      {isPending ? (
        <LoadingState label="Loading job…" />
      ) : error ? (
        <ErrorState error={error} onRetry={refetch} />
      ) : (
        <>
          <JobSummary job={job} />
          {job.status === 'Completed' && <JobTreeSection jobId={job.jobId} />}
        </>
      )}
    </section>
  );
}

function JobSummary({ job }: { job: Job }) {
  const cancelJob = useCancelJob(job.jobId);

  return (
    <div className="card">
      <dl className="details">
        <dt>Status</dt>
        <dd>
          <StatusBadge status={job.status} />
        </dd>
        <dt>URL</dt>
        <dd>
          <a href={job.url} target="_blank" rel="noreferrer">
            {job.url}
          </a>
        </dd>
        <dt>Limits</dt>
        <dd>
          depth {job.maxDepth}, up to {job.maxPages} pages
        </dd>
        <dt>Created</dt>
        <dd>{formatDateTime(job.createdAt)}</dd>
        <dt>Started</dt>
        <dd>{formatDateTime(job.startedAt)}</dd>
        <dt>Completed</dt>
        <dd>{formatDateTime(job.completedAt)}</dd>
        {job.failureReason && (
          <>
            <dt>Failure reason</dt>
            <dd className="field-error">{job.failureReason}</dd>
          </>
        )}
      </dl>
      <JobProgress counts={job.pageCounts} />
      {isActiveStatus(job.status) && (
        <button type="button" onClick={() => cancelJob.mutate()} disabled={cancelJob.isPending}>
          {cancelJob.isPending ? 'Canceling…' : 'Cancel job'}
        </button>
      )}
      {cancelJob.error && <ErrorState error={cancelJob.error} />}
    </div>
  );
}

function JobTreeSection({ jobId }: { jobId: string }) {
  const { data, error, isPending, refetch } = useJobTree(jobId);

  return (
    <section aria-labelledby="tree-heading">
      <h2 id="tree-heading">Page tree</h2>
      {isPending ? (
        <LoadingState label="Loading page tree…" />
      ) : error ? (
        <ErrorState error={error} onRetry={refetch} />
      ) : data.root ? (
        <PageTree jobId={jobId} root={data.root} />
      ) : (
        <EmptyState>No pages were crawled for this job.</EmptyState>
      )}
    </section>
  );
}
