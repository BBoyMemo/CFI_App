import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';

/**
 * The one card everybody has, and the operator's whole reason for opening the app.
 *
 * Four lines of copy became one: the card is the biggest thing on the screen and it is
 * yellow, so what it does is already obvious - explaining it only slowed the read.
 * Still big enough to hit with a glove on.
 */
export default function ReportHero() {
  const { t } = useTranslation();

  return (
    <Link
      to="/workorders/new"
      className="block rounded-lg bg-cfi-yellow p-5 transition-colors hover:bg-cfi-yellow-dark"
    >
      <h2 className="text-2xl font-bold text-cfi-brown-dark">{t('dashboard.reportEyebrow')}</h2>
    </Link>
  );
}
