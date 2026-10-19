export const queryKeys = {
  job: (jobId: string) => ['jobs', jobId] as const,
  jobTree: (jobId: string) => ['jobs', jobId, 'tree'] as const,
  pageLinks: (jobId: string, pageId: number) => ['jobs', jobId, 'pages', pageId, 'links'] as const,
  jobHistory: (page: number, pageSize: number) => ['jobs', 'history', page, pageSize] as const,
};
