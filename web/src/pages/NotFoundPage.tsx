import { Link } from 'react-router-dom';
import { EmptyState } from '../components/EmptyState';

export function NotFoundPage() {
  return (
    <EmptyState>
      Page not found. <Link to="/">Start a crawl</Link>.
    </EmptyState>
  );
}
