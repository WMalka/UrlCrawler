export type JobStatus = 'Pending' | 'Running' | 'Completed' | 'Failed' | 'Canceled';
export type PageStatus = 'Queued' | 'Done' | 'Skipped' | 'Failed';

export interface PageCounts {
  total: number;
  queued: number;
  done: number;
  skipped: number;
  failed: number;
}

export interface Job {
  jobId: string;
  url: string;
  status: JobStatus;
  maxDepth: number;
  maxPages: number;
  createdAt: string;
  startedAt: string | null;
  completedAt: string | null;
  failureReason: string | null;
  pageCounts: PageCounts;
}

export interface PageNode {
  pageId: number;
  url: string;
  depth: number;
  status: PageStatus;
  linkCount: number | null;
  internalLinkCount: number | null;
  domainLinkRatio: number | null;
  error: string | null;
  children: PageNode[];
}

export interface JobTree {
  jobId: string;
  status: JobStatus;
  root: PageNode | null;
}

export interface PageLink {
  url: string;
  isInternal: boolean;
}

export interface JobSummary {
  jobId: string;
  url: string;
  status: JobStatus;
  createdAt: string;
  completedAt: string | null;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface CreateJobRequest {
  url: string;
  maxDepth?: number;
}

export interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
}

export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly problem: ProblemDetails,
  ) {
    super(problem.detail ?? problem.title ?? `Request failed with status ${status}`);
    this.name = 'ApiError';
  }
}

export const isActiveStatus = (status: JobStatus | undefined) =>
  status === 'Pending' || status === 'Running';

const API_BASE = '/api';

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  let response: Response;
  try {
    response = await fetch(`${API_BASE}${path}`, {
      ...init,
      headers: { Accept: 'application/json', 'Content-Type': 'application/json', ...init?.headers },
    });
  } catch {
    throw new ApiError(0, { title: 'Network error', detail: 'Could not reach the crawler API.' });
  }

  if (!response.ok) {
    const problem: ProblemDetails = await response.json().catch(() => ({}));
    throw new ApiError(response.status, { title: response.statusText, ...problem });
  }

  return response.status === 204 ? (undefined as T) : ((await response.json()) as T);
}

export const crawlerApi = {
  createJob: (body: CreateJobRequest) =>
    request<{ jobId: string }>('/jobs', { method: 'POST', body: JSON.stringify(body) }),
  getJob: (jobId: string) => request<Job>(`/jobs/${encodeURIComponent(jobId)}`),
  getJobTree: (jobId: string) => request<JobTree>(`/jobs/${encodeURIComponent(jobId)}/tree`),
  getPageLinks: (jobId: string, pageId: number) =>
    request<PageLink[]>(`/jobs/${encodeURIComponent(jobId)}/pages/${pageId}/links`),
  getJobs: (page: number, pageSize: number) =>
    request<PagedResult<JobSummary>>(`/jobs?page=${page}&pageSize=${pageSize}`),
  cancelJob: (jobId: string) =>
    request<void>(`/jobs/${encodeURIComponent(jobId)}/cancel`, { method: 'POST' }),
};
