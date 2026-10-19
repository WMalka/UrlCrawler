const dateFormat = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'medium' });

export const formatDateTime = (iso: string | null) => (iso ? dateFormat.format(new Date(iso)) : '—');

export const formatPercent = (ratio: number | null) =>
  ratio === null ? '—' : `${(ratio * 100).toFixed(1)}%`;
