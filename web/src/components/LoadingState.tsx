export function LoadingState({ label = 'Loading…' }: { label?: string }) {
  return (
    <p className="state state-loading" role="status">
      {label}
    </p>
  );
}
