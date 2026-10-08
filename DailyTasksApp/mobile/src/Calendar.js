import React, {useMemo, useState} from 'react';
import {Pressable, StyleSheet, Text, View} from 'react-native';
import {useTranslation} from 'react-i18next';
import {siteToday} from './format';
import {colors} from './theme';
import {Button, IconButton, Sheet} from './ui';

const pad = n => String(n).padStart(2, '0');
const iso = (y, m, d) => `${y}-${pad(m + 1)}-${pad(d)}`;

function safeFormat(locale, options, date, fallback) {
  try {
    return new Intl.DateTimeFormat(locale, options).format(date);
  } catch {
    return fallback;
  }
}

// Month grid for picking a single day (yyyy-mm-dd). Weeks start on Monday, as in the UK.
// Days outside [min, max] are shown but cannot be picked.
export default function CalendarSheet({value, min, max, onSelect, onClose}) {
  const {t, i18n} = useTranslation();
  const today = siteToday();
  const start = value || today;
  const [month, setMonth] = useState({y: Number(start.slice(0, 4)), m: Number(start.slice(5, 7)) - 1});

  const weekdays = useMemo(
    () =>
      // 2024-01-01 was a Monday.
      [0, 1, 2, 3, 4, 5, 6].map(i =>
        safeFormat(i18n.language, {weekday: 'narrow', timeZone: 'UTC'}, new Date(Date.UTC(2024, 0, 1 + i)), 'MTWTFSS'[i]),
      ),
    [i18n.language],
  );

  const cells = useMemo(() => {
    const first = new Date(Date.UTC(month.y, month.m, 1));
    const lead = (first.getUTCDay() + 6) % 7;
    const days = new Date(Date.UTC(month.y, month.m + 1, 0)).getUTCDate();
    const list = Array.from({length: lead}, () => null);
    for (let d = 1; d <= days; d++) {
      list.push(iso(month.y, month.m, d));
    }
    while (list.length % 7 !== 0) {
      list.push(null);
    }
    return list;
  }, [month]);

  const title = safeFormat(
    i18n.language,
    {month: 'long', year: 'numeric', timeZone: 'UTC'},
    new Date(Date.UTC(month.y, month.m, 15)),
    `${month.y}-${pad(month.m + 1)}`,
  );
  const step = delta =>
    setMonth(({y, m}) => {
      const next = m + delta;
      return {y: y + Math.floor(next / 12), m: ((next % 12) + 12) % 12};
    });

  const pick = day => {
    onSelect(day);
    onClose();
  };
  const todayAllowed = (!min || today >= min) && (!max || today <= max);

  return (
    <Sheet
      title={t('tasks.date')}
      onClose={onClose}
      footer={
        <>
          <Button title={t('common.cancel')} variant="ghost" onPress={onClose} style={styles.flex} />
          <Button title={t('common.today')} onPress={() => pick(today)} disabled={!todayAllowed} style={styles.flex} />
        </>
      }>
      <View style={styles.monthBar}>
        <IconButton icon="left" size={26} color={colors.brown} onPress={() => step(-1)} label="‹" />
        <Text style={styles.monthTitle}>{title}</Text>
        <IconButton icon="right" size={26} color={colors.brown} onPress={() => step(1)} label="›" />
      </View>
      <View style={styles.grid}>
        {weekdays.map((w, i) => (
          <Text key={`w${i}`} style={styles.weekday}>
            {w}
          </Text>
        ))}
        {cells.map((day, i) => {
          if (!day) {
            return <View key={`e${i}`} style={styles.cell} />;
          }
          const disabled = (min && day < min) || (max && day > max);
          const selected = day === value;
          const isToday = day === today;
          return (
            <Pressable
              key={day}
              style={styles.cell}
              disabled={disabled}
              onPress={() => pick(day)}
              accessibilityState={{selected, disabled}}>
              <View style={[styles.day, isToday && styles.today, selected && styles.selected]}>
                <Text
                  style={[
                    styles.dayText,
                    disabled && styles.dayDisabled,
                    isToday && styles.todayText,
                    selected && styles.selectedText,
                  ]}>
                  {Number(day.slice(8))}
                </Text>
              </View>
            </Pressable>
          );
        })}
      </View>
    </Sheet>
  );
}

const styles = StyleSheet.create({
  flex: {flex: 1},
  monthBar: {flexDirection: 'row', alignItems: 'center', marginBottom: 6},
  monthTitle: {flex: 1, textAlign: 'center', fontSize: 16, fontWeight: '700', color: colors.brown},
  grid: {flexDirection: 'row', flexWrap: 'wrap'},
  weekday: {width: `${100 / 7}%`, textAlign: 'center', color: colors.muted, fontWeight: '700', paddingVertical: 6},
  cell: {width: `${100 / 7}%`, aspectRatio: 1, alignItems: 'center', justifyContent: 'center'},
  day: {width: 40, height: 40, borderRadius: 20, alignItems: 'center', justifyContent: 'center'},
  today: {borderWidth: 2, borderColor: colors.yellow},
  selected: {backgroundColor: colors.brown, borderColor: colors.brown},
  dayText: {fontSize: 16, color: colors.ink},
  dayDisabled: {color: colors.creamDark},
  todayText: {fontWeight: '800', color: colors.brown},
  selectedText: {color: colors.cream, fontWeight: '800'},
});
