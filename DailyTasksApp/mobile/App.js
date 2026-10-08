import React, {useState} from 'react';
import {
  ActivityIndicator,
  Pressable,
  SafeAreaView,
  StatusBar,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import {useTranslation} from 'react-i18next';
import {GestureHandlerRootView} from 'react-native-gesture-handler';
import './src/i18n';
import {AuthProvider, useAuth} from './src/auth';
import LanguagePicker from './src/LanguagePicker';
import AccountScreen from './src/screens/AccountScreen';
import HistoryScreen from './src/screens/HistoryScreen';
import LoginScreen from './src/screens/LoginScreen';
import OrdersScreen from './src/screens/OrdersScreen';
import TasksScreen from './src/screens/TasksScreen';
import TeamScreen from './src/screens/TeamScreen';
import {colors} from './src/theme';
import {Glyph} from './src/ui';

export default function App() {
  return (
    <GestureHandlerRootView style={styles.fill}>
      <AuthProvider>
        <StatusBar backgroundColor={colors.brown} barStyle="light-content" />
        <Root />
      </AuthProvider>
    </GestureHandlerRootView>
  );
}

function Root() {
  const {ready, user} = useAuth();
  if (!ready) {
    return (
      <View
        style={[styles.fill, styles.center, {backgroundColor: colors.brown}]}>
        <ActivityIndicator color={colors.yellow} size="large" />
      </View>
    );
  }
  return user ? <Main /> : <LoginScreen />;
}

function Main() {
  const {t} = useTranslation();
  const {user, isManager, logout} = useAuth();
  const [tab, setTab] = useState('tasks');

  const tabs = [
    {key: 'tasks', icon: 'tasks', label: t('nav.tasks')},
    {key: 'history', icon: 'history', label: t('nav.history')},
    {key: 'orders', icon: 'orders', label: t('nav.orders')},
    ...(isManager ? [{key: 'team', icon: 'team', label: t('nav.team')}] : []),
  ];

  return (
    <SafeAreaView style={[styles.fill, {backgroundColor: colors.cream}]}>
      <View style={styles.header}>
        <Pressable
          style={[styles.userButton, tab === 'account' && styles.userButtonOn]}
          onPress={() => setTab('account')}
          accessibilityLabel={t('nav.account')}>
          <Text style={styles.userName} numberOfLines={1}>
            👤 {user.name}
          </Text>
        </Pressable>
        <LanguagePicker dark />
        <Pressable
          onPress={logout}
          hitSlop={8}
          style={styles.logout}
          accessibilityLabel={t('nav.logout')}>
          <Glyph name="logout" size={22} color={colors.cream} />
        </Pressable>
      </View>

      <View style={styles.fill}>
        {tab === 'tasks' ? <TasksScreen /> : null}
        {tab === 'history' ? <HistoryScreen /> : null}
        {tab === 'orders' ? <OrdersScreen /> : null}
        {tab === 'team' && isManager ? <TeamScreen /> : null}
        {tab === 'account' ? <AccountScreen /> : null}
      </View>

      <View style={styles.tabBar}>
        {tabs.map(x => {
          const on = tab === x.key;
          return (
            <Pressable
              key={x.key}
              style={styles.tab}
              onPress={() => setTab(x.key)}
              accessibilityState={{selected: on}}>
              <View style={[styles.tabIcon, on && styles.tabIconOn]}>
                <Glyph
                  name={x.icon}
                  size={20}
                  color={on ? colors.brown : colors.muted}
                />
              </View>
              <Text style={[styles.tabText, on && styles.tabTextOn]}>
                {x.label}
              </Text>
            </Pressable>
          );
        })}
      </View>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  fill: {flex: 1},
  center: {alignItems: 'center', justifyContent: 'center'},
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
    backgroundColor: colors.brown,
    paddingHorizontal: 14,
    paddingVertical: 10,
  },
  userButton: {
    flex: 1,
    borderRadius: 8,
    paddingHorizontal: 6,
    paddingVertical: 4,
  },
  userButtonOn: {backgroundColor: 'rgba(245,239,226,0.15)'},
  userName: {color: colors.cream, fontWeight: '600'},
  logout: {padding: 4},
  tabBar: {
    flexDirection: 'row',
    backgroundColor: colors.white,
    borderTopWidth: 1,
    borderTopColor: colors.creamDark,
  },
  tab: {flex: 1, alignItems: 'center', paddingVertical: 6, gap: 2},
  tabIcon: {paddingHorizontal: 16, paddingVertical: 3, borderRadius: 999},
  tabIconOn: {backgroundColor: colors.yellowSoft},
  tabText: {fontSize: 12, color: colors.muted, fontWeight: '600'},
  tabTextOn: {color: colors.brown},
});
