import { useId, useState } from 'react';
import type { PageNode } from '../api/crawlerApi';
import { formatPercent } from '../format';
import { PageLinks } from './PageLinks';
import { StatusBadge } from './StatusBadge';

interface PageTreeNodeProps {
  jobId: string;
  node: PageNode;
}

export function PageTreeNode({ jobId, node }: PageTreeNodeProps) {
  const [expanded, setExpanded] = useState(node.depth === 0);
  const [showLinks, setShowLinks] = useState(false);
  const childrenId = useId();
  const linksId = useId();
  const hasChildren = node.children.length > 0;

  return (
    <li className="tree-node">
      <div className="tree-row">
        {hasChildren ? (
          <button
            type="button"
            className="toggle"
            aria-expanded={expanded}
            aria-controls={childrenId}
            aria-label={`${expanded ? 'Collapse' : 'Expand'} ${node.url}`}
            onClick={() => setExpanded((value) => !value)}
          >
            {expanded ? '▾' : '▸'}
          </button>
        ) : (
          <span className="toggle" aria-hidden="true" />
        )}
        <a href={node.url} target="_blank" rel="noreferrer" className="tree-url">
          {node.url}
        </a>
        <StatusBadge status={node.status} />
        <span className="muted">depth {node.depth}</span>
        <span title="Domain Link Ratio: internal links / total links">DLR {formatPercent(node.domainLinkRatio)}</span>
        {node.linkCount !== null && (
          <button
            type="button"
            className="link-button"
            aria-expanded={showLinks}
            aria-controls={linksId}
            onClick={() => setShowLinks((value) => !value)}
          >
            {node.linkCount} links ({node.internalLinkCount ?? 0} internal)
          </button>
        )}
      </div>
      {node.error && <p className="node-error">Error: {node.error}</p>}
      {showLinks && (
        <div id={linksId} className="tree-links">
          <PageLinks jobId={jobId} pageId={node.pageId} />
        </div>
      )}
      {hasChildren && expanded && (
        <ul id={childrenId} className="tree">
          {node.children.map((child) => (
            <PageTreeNode key={child.pageId} jobId={jobId} node={child} />
          ))}
        </ul>
      )}
    </li>
  );
}
