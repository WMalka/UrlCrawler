import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { crawlerApi } from '../api/crawlerApi';
import { queryKeys } from './queryKeys';

export function useJobHistory(page: number, pageSize: number) {
  return useQuery({
    queryKey: queryKeys.jobHistory(page, pageSize),
    queryFn: () => crawlerApi.getJobs(page, pageSize),
    placeholderData: keepPreviousData,
  });
}
