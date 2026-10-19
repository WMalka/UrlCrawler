import { NavLink } from 'react-router-dom';

export function NavBar() {
  return (
    <header className="navbar">
      <span className="brand">Web Crawler</span>
      <nav aria-label="Main">
        <NavLink to="/" end>
          Start Crawl
        </NavLink>
        <NavLink to="/history">History</NavLink>
      </nav>
    </header>
  );
}
