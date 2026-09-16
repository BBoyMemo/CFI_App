export default function Card({ className = '', children, ...props }) {
  return (
    <div
      className={`rounded-lg border border-cfi-rule bg-cfi-surface p-4 sm:p-5 ${className}`}
      {...props}
    >
      {children}
    </div>
  );
}
