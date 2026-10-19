import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { ApiError } from '../api/crawlerApi';
import { ErrorState } from '../components/ErrorState';
import { useCreateJob } from '../hooks/useCreateJob';

type FieldErrors = { url?: string[]; maxDepth?: string[] };

function validate(url: string, maxDepth: string): FieldErrors {
  const errors: FieldErrors = {};
  try {
    const parsed = new URL(url);
    if (parsed.protocol !== 'http:' && parsed.protocol !== 'https:') {
      errors.url = ['URL must use http or https.'];
    }
  } catch {
    errors.url = ['Enter an absolute URL, e.g. https://example.com.'];
  }
  if (maxDepth !== '' && !/^[0-5]$/.test(maxDepth)) {
    errors.maxDepth = ['Max depth must be a whole number between 0 and 5.'];
  }
  return errors;
}

function serverFieldErrors(error: unknown): FieldErrors | undefined {
  if (!(error instanceof ApiError) || !error.problem.errors) return undefined;
  const { Url, MaxDepth } = error.problem.errors;
  return { url: Url, maxDepth: MaxDepth };
}

export function StartCrawlPage() {
  const navigate = useNavigate();
  const createJob = useCreateJob();
  const [url, setUrl] = useState('');
  const [maxDepth, setMaxDepth] = useState('');
  const [clientErrors, setClientErrors] = useState<FieldErrors>({});

  const serverErrors = serverFieldErrors(createJob.error);
  const urlErrors = clientErrors.url ?? serverErrors?.url;
  const maxDepthErrors = clientErrors.maxDepth ?? serverErrors?.maxDepth;

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const trimmedUrl = url.trim();
    const errors = validate(trimmedUrl, maxDepth);
    setClientErrors(errors);
    if (errors.url || errors.maxDepth) return;

    createJob.mutate(
      { url: trimmedUrl, maxDepth: maxDepth === '' ? undefined : Number(maxDepth) },
      { onSuccess: ({ jobId }) => navigate(`/jobs/${jobId}`) },
    );
  };

  return (
    <section>
      <h1>Start a crawl</h1>
      <form className="form" onSubmit={handleSubmit} noValidate>
        <div className="field">
          <label htmlFor="url">Start URL</label>
          <input
            id="url"
            type="url"
            value={url}
            onChange={(event) => setUrl(event.target.value)}
            placeholder="https://www.scrapethissite.com/pages/"
            required
            aria-invalid={!!urlErrors}
            aria-describedby={urlErrors ? 'url-error' : undefined}
          />
          {urlErrors && (
            <p id="url-error" className="field-error">
              {urlErrors.join(' ')}
            </p>
          )}
        </div>
        <div className="field">
          <label htmlFor="maxDepth">Max depth (0–5, optional)</label>
          <input
            id="maxDepth"
            type="number"
            min={0}
            max={5}
            step={1}
            value={maxDepth}
            onChange={(event) => setMaxDepth(event.target.value)}
            placeholder="2"
            aria-invalid={!!maxDepthErrors}
            aria-describedby={maxDepthErrors ? 'maxDepth-hint maxDepth-error' : 'maxDepth-hint'}
          />
          <p id="maxDepth-hint" className="muted">
            Leave empty to use the server default (2).
          </p>
          {maxDepthErrors && (
            <p id="maxDepth-error" className="field-error">
              {maxDepthErrors.join(' ')}
            </p>
          )}
        </div>
        <button type="submit" disabled={createJob.isPending}>
          {createJob.isPending ? 'Starting…' : 'Start crawl'}
        </button>
      </form>
      {createJob.error && !serverErrors && <ErrorState error={createJob.error} />}
    </section>
  );
}
