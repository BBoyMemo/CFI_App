/**
 * The line icons the approved prototypes use, inline rather than from a font or package.
 *
 * Two reasons they live here: the project deliberately leans on icons over words ("az
 * yazı, çok ikon"), so these are load-bearing rather than decoration; and shipping the
 * eight shapes actually used beats pulling in an icon library for them.
 */
const PATHS = {
  home: <><path d="M4 11l8-6 8 6v8a1 1 0 0 1-1 1h-5v-6H10v6H5a1 1 0 0 1-1-1z" /></>,
  calendar: <><rect x="4" y="5" width="16" height="16" rx="2.5" /><path d="M8 3v4M16 3v4M4 10h16" /></>,
  clock: <><path d="M12 7v5l3.2 2" /><circle cx="12" cy="12" r="8.5" /></>,
  bell: <><path d="M6 10a6 6 0 0 1 12 0c0 4 1.5 5.5 1.5 5.5H4.5S6 14 6 10z" /><path d="M10 19a2 2 0 0 0 4 0" /></>,
  sun: (
    <>
      <circle cx="12" cy="12" r="4" />
      <path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4" />
    </>
  ),
  clipboard: <><rect x="6" y="4" width="12" height="17" rx="2" /><path d="M9 4h6v3H9zM9 11h6M9 15h4" /></>,
  box: <><path d="M3 8l9-5 9 5v8l-9 5-9-5z" /><path d="M3 8l9 5 9-5M12 13v8" /></>,
  history: <><path d="M3 12a9 9 0 1 0 3-6.7M3 4v4h4" /><path d="M12 8v4.5l3 1.8" /></>,
  users: <><circle cx="9" cy="8" r="3.2" /><path d="M3 20c0-3.3 2.7-5.5 6-5.5s6 2.2 6 5.5" /><path d="M16 5.5a3.2 3.2 0 0 1 0 6M17.5 20c0-2.4-1-4.3-2.5-5.3" /></>,
  wrench: <path d="M20 5.5a5 5 0 0 1-6.6 6.6L6 19.5 4.5 18l7.4-7.4A5 5 0 0 1 18.5 4z" />,
  message: <><path d="M4 6h16v10H9l-5 4z" /><path d="M8 10h8M8 13h5" /></>,
  settings: <><circle cx="12" cy="12" r="3" /><path d="M12 3v2.5M12 18.5V21M4.2 7.5l2.2 1.3M17.6 15.2l2.2 1.3M4.2 16.5l2.2-1.3M17.6 8.8l2.2-1.3" /></>,
  person: <><circle cx="12" cy="8" r="3.5" /><path d="M5 20c0-3.6 3.1-6 7-6s7 2.4 7 6" /></>,
};

export default function Icon({ name, size = 18, className = '' }) {
  const path = PATHS[name];
  if (!path) return null;

  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      strokeLinecap="round"
      strokeLinejoin="round"
      className={className}
      aria-hidden="true"
    >
      {path}
    </svg>
  );
}
