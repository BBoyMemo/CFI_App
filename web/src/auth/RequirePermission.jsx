import { Navigate } from 'react-router-dom';

import { useAuth } from './useAuth';

/**
 * Frontend route protection only improves the experience - it stops a user from
 * following a link to a screen they cannot use. It is not security: the backend enforces
 * the real rule on every request regardless of what this component decides.
 */
export default function RequirePermission({ permission, anyOf, children }) {
  const { hasPermission } = useAuth();

  // `anyOf` is for the screen two different jobs open - Holiday, where one person books
  // leave and another approves it. Holding either is enough to be let in; which half of
  // the page they then see is the page's own decision.
  const allowed = anyOf
    ? anyOf.some((candidate) => hasPermission(candidate))
    : hasPermission(permission);

  if (!allowed) {
    return <Navigate to="/" replace />;
  }

  return children;
}
