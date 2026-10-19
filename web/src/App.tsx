import { Route, Routes } from 'react-router-dom';
import { NavBar } from './components/NavBar';
import { HistoryPage } from './pages/HistoryPage';
import { JobDetailsPage } from './pages/JobDetailsPage';
import { NotFoundPage } from './pages/NotFoundPage';
import { StartCrawlPage } from './pages/StartCrawlPage';

export function App() {
  return (
    <>
      <NavBar />
      <main className="container">
        <Routes>
          <Route path="/" element={<StartCrawlPage />} />
          <Route path="/jobs/:jobId" element={<JobDetailsPage />} />
          <Route path="/history" element={<HistoryPage />} />
          <Route path="*" element={<NotFoundPage />} />
        </Routes>
      </main>
    </>
  );
}
