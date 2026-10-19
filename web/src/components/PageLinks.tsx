import { usePageLinks } from '../hooks/usePageLinks';
import { EmptyState } from './EmptyState';
import { ErrorState } from './ErrorState';
import { LoadingState } from './LoadingState';

export function PageLinks({ jobId, pageId }: { jobId: string; pageId: number }) {
  const { data, error, isPending, refetch } = usePageLinks(jobId, pageId);

  if (isPending) return <LoadingState label="Loading links…" />;
  if (error) return <ErrorState error={error} onRetry={refetch} />;
  if (data.length === 0) return <EmptyState>No outgoing links.</EmptyState>;

  return (
    <ul className="links">
      {data.map((link) => (
        <li key={link.url}>
          <span className={link.isInternal ? 'tag tag-internal' : 'tag tag-external'}>
            {link.isInternal ? 'internal' : 'external'}
          </span>{' '}
          <a href={link.url} target="_blank" rel="noreferrer">
            {link.url}
          </a>
        </li>
      ))}
    </ul>
  );
}
