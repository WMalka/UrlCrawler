import { useMutation, useQueryClient } from '@tanstack/react-query';
import { crawlerApi } from '../api/crawlerApi';

export function useCreateJob() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: crawlerApi.createJob,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['jobs', 'history'] }),
  });
}
