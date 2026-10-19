import { useQuery } from '@tanstack/react-query';
import { crawlerApi } from '../api/crawlerApi';
import { queryKeys } from './queryKeys';

export function usePageLinks(jobId: string, pageId: number) {
  return useQuery({
    queryKey: queryKeys.pageLinks(jobId, pageId),
    queryFn: () => crawlerApi.getPageLinks(jobId, pageId),
    staleTime: Infinity,
  });
}
