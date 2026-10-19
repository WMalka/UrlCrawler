import { useQuery } from '@tanstack/react-query';
import { crawlerApi, isActiveStatus } from '../api/crawlerApi';
import { queryKeys } from './queryKeys';

const POLL_INTERVAL_MS = 1500;

export function useJob(jobId: string) {
  return useQuery({
    queryKey: queryKeys.job(jobId),
    queryFn: () => crawlerApi.getJob(jobId),
    refetchInterval: (query) => (isActiveStatus(query.state.data?.status) ? POLL_INTERVAL_MS : false),
  });
}
