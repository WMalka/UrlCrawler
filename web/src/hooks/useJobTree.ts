import { useQuery } from '@tanstack/react-query';
import { crawlerApi } from '../api/crawlerApi';
import { queryKeys } from './queryKeys';

export function useJobTree(jobId: string) {
  return useQuery({
    queryKey: queryKeys.jobTree(jobId),
    queryFn: () => crawlerApi.getJobTree(jobId),
    staleTime: Infinity,
  });
}
