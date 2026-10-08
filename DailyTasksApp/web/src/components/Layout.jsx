import { NavLink, Outlet } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../auth/AuthContext';
import Icon from './Icon';
import LanguageSelect from './LanguageSelect';

export default function Layout() {
  const { t } = useTranslation();
  const { user, isManager, logout } = useAuth();

  const links = [
    { to: '/tasks', icon: 'tasks', label: t('nav.tasks') },
    { to: '/history', icon: 'history', label: t('nav.history') },
    { to: '/orders', icon: 'orders', label: t('nav.orders') },
    ...(isManager ? [{ to: '/team', icon: 'team', label: t('nav.team') }] : []),
  ];

  return (
    <div className="min-h-screen pb-20 sm:pb-0">
      <header className="sticky top-0 z-30 bg-brown text-cream shadow">
        <div className="mx-auto flex max-w-4xl items-center gap-3 px-4 py-2.5">
          <div className="flex items-center gap-2 font-bold">
            <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-yellow text-brown">
              <Icon name="check" size={18} strokeWidth={3} />
            </span>
            <span className="hidden sm:inline">{t('app.name')}</span>
          </div>

          <nav className="ml-4 hidden gap-1 sm:flex">
            {links.map((l) => (
              <NavLink
                key={l.to}
                to={l.to}
                className={({ isActive }) =>
                  `flex items-center gap-2 rounded-lg px-3 py-1.5 text-sm font-medium ${
                    isActive ? 'bg-cream/15 text-yellow' : 'text-cream/80 hover:text-cream'
                  }`
                }
              >
                <Icon name={l.icon} size={18} />
                {l.label}
              </NavLink>
            ))}
          </nav>

          <div className="ml-auto flex items-center gap-1">
            <LanguageSelect dark />
            <NavLink
              to="/account"
              title={t('nav.account')}
              aria-label={t('nav.account')}
              className={({ isActive }) =>
                `flex h-9 items-center gap-1.5 rounded-lg px-2 text-sm ${
                  isActive ? 'bg-cream/15 text-yellow' : 'text-cream/80 hover:bg-cream/10 hover:text-cream'
                }`
              }
            >
              <Icon name="user" size={18} />
              <span className="hidden md:inline">{user?.name}</span>
            </NavLink>
            <button
              type="button"
              onClick={logout}
              className="flex h-9 w-9 items-center justify-center rounded-lg text-cream/80 hover:bg-cream/10 hover:text-cream"
              aria-label={t('nav.logout')}
              title={t('nav.logout')}
            >
              <Icon name="logout" />
            </button>
          </div>
        </div>
      </header>

      {/* Bottom padding keeps the floating "+" button clear of the last card. */}
      <main className="mx-auto max-w-4xl px-4 pt-4 pb-24">
        <Outlet />
      </main>

      {/* Bottom tab bar on phones. */}
      <nav className="fixed inset-x-0 bottom-0 z-30 flex border-t border-cream-dark bg-white pb-[env(safe-area-inset-bottom)] sm:hidden">
        {links.map((l) => (
          <NavLink
            key={l.to}
            to={l.to}
            className={({ isActive }) =>
              `flex flex-1 flex-col items-center gap-0.5 py-2 text-xs font-medium ${
                isActive ? 'text-brown' : 'text-muted'
              }`
            }
          >
            {({ isActive }) => (
              <>
                <span className={`rounded-full px-4 py-1 ${isActive ? 'bg-yellow/30' : ''}`}>
                  <Icon name={l.icon} size={22} />
                </span>
                {l.label}
              </>
            )}
          </NavLink>
        ))}
      </nav>
    </div>
  );
}
