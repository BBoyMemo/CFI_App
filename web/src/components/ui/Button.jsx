const variants = {
  primary: 'bg-cfi-yellow text-cfi-brown-dark hover:bg-cfi-yellow-dark hover:text-white',
  secondary: 'border border-cfi-rule bg-cfi-sunk text-cfi-brown-dark hover:bg-cfi-rule',
  danger: 'bg-cfi-red text-white hover:bg-cfi-red/85',
  ghost: 'text-cfi-brown-dark hover:bg-cfi-sunk',
};

/**
 * Sized for a gloved finger on a factory floor tablet, not a mouse pointer - the 44px
 * minimum height is deliberate, not a default left unconsidered.
 */
export default function Button({ variant = 'primary', className = '', ...props }) {
  return (
    <button
      type="button"
      className={`inline-flex min-h-11 items-center justify-center gap-2 rounded px-4 font-semibold transition-colors disabled:cursor-not-allowed disabled:opacity-50 ${variants[variant]} ${className}`}
      {...props}
    />
  );
}
