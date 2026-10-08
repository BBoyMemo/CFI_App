import { Navigate, Route, Routes } from 'react-router-dom';
import { useAuth } from './auth/AuthContext';
import Layout from './components/Layout';
import AccountPage from './pages/AccountPage';
import LoginPage from './pages/LoginPage';
import OrdersPage from './pages/orders/OrdersPage';
import HistoryPage from './pages/tasks/HistoryPage';
import TasksPage from './pages/tasks/TasksPage';
import TeamPage from './pages/TeamPage';

export default function App() {
  const { user, isManager } = useAuth();

  if (!user) return <LoginPage />;

  return (
    <Routes>
      <Route element={<Layout />}>
        <Route path="/tasks" element={<TasksPage />} />
        <Route path="/history" element={<HistoryPage />} />
        <Route path="/orders" element={<OrdersPage />} />
        {isManager && <Route path="/team" element={<TeamPage />} />}
        <Route path="/account" element={<AccountPage />} />
        <Route path="*" element={<Navigate to="/tasks" replace />} />
      </Route>
    </Routes>
  );
}
