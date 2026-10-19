import { useMutation, useQueryClient } from '@tanstack/react-query';
import { crawlerApi } from '../api/crawlerApi';
import { queryKeys } from './queryKeys';

export function useCancelJob(jobId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => crawlerApi.cancelJob(jobId),
    onSettled: () => queryClient.invalidateQueries({ queryKey: queryKeys.job(jobId), exact: true }),
  });
}
