import { ApiError } from '../api/crawlerApi';

interface ErrorStateProps {
  error: unknown;
  onRetry?: () => void;
}

export function ErrorState({ error, onRetry }: ErrorStateProps) {
  const title = error instanceof ApiError ? (error.problem.title ?? 'Request failed') : 'Something went wrong';
  const detail = error instanceof ApiError ? error.problem.detail : error instanceof Error ? error.message : undefined;

  return (
    <div className="state state-error" role="alert">
      <strong>{title}</strong>
      {detail && <p>{detail}</p>}
      {onRetry && (
        <button type="button" onClick={onRetry}>
          Retry
        </button>
      )}
    </div>
  );
}
