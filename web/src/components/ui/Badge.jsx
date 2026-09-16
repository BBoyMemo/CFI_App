export default function Badge({ tone = 'bg-cfi-sunk text-cfi-brown-dark', children }) {
  return (
    <span className={`inline-flex items-center rounded px-2 py-0.5 text-xs font-semibold whitespace-nowrap ${tone}`}>
      {children}
    </span>
  );
}
