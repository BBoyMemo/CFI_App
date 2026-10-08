import React, {useState} from 'react';
import {Image, Modal, Pressable, StyleSheet, Text, View} from 'react-native';
import {launchCamera, launchImageLibrary} from 'react-native-image-picker';
import {useTranslation} from 'react-i18next';
import {photoSource} from './api';
import {colors} from './theme';
import {Glyph} from './ui';

// Resized on the phone before upload: fast on factory wifi, well under the 10 MB limit.
const PICKER_OPTIONS = {
  mediaType: 'photo',
  maxWidth: 1920,
  maxHeight: 1920,
  quality: 0.85,
  includeBase64: false,
  saveToPhotos: false,
};

export function PhotoInput({photo, onChange, error}) {
  const {t} = useTranslation();

  async function pick(fromCamera) {
    try {
      const result = fromCamera
        ? await launchCamera(PICKER_OPTIONS)
        : await launchImageLibrary(PICKER_OPTIONS);
      const asset = result.assets?.[0];
      if (asset?.uri) {
        onChange(asset);
      }
    } catch {
      // Picker closed or camera unavailable: nothing to do.
    }
  }

  if (photo) {
    return (
      <View>
        <View style={styles.previewWrap}>
          <Image source={{uri: photo.uri}} style={styles.preview} />
          <Pressable
            style={styles.remove}
            onPress={() => onChange(null)}
            accessibilityLabel={t('photo.remove')}>
            <Text style={styles.removeText}>✕</Text>
          </Pressable>
        </View>
        {error ? <Text style={styles.error}>{error}</Text> : null}
      </View>
    );
  }

  return (
    <View>
      <View style={styles.row}>
        <Pressable style={styles.pickButton} onPress={() => pick(true)}>
          <Glyph name="camera" size={24} />
          <Text style={styles.pickText}>{t('photo.camera')}</Text>
        </Pressable>
        <Pressable style={styles.pickButton} onPress={() => pick(false)}>
          <Glyph name="gallery" size={24} />
          <Text style={styles.pickText}>{t('photo.gallery')}</Text>
        </Pressable>
      </View>
      {error ? <Text style={styles.error}>{error}</Text> : null}
    </View>
  );
}

// Several photos for one record: what is already stored plus what was just picked.
// `stored` = [{id, path}], `added` = picker assets. The parent decides what happens on save.
export function MultiPhotoInput({
  stored,
  added,
  onRemoveStored,
  onAdd,
  onRemoveAdded,
  max,
  error,
}) {
  const {t} = useTranslation();
  const room = max - stored.length - added.length;

  async function pick(fromCamera) {
    try {
      const result = fromCamera
        ? await launchCamera(PICKER_OPTIONS)
        : await launchImageLibrary({...PICKER_OPTIONS, selectionLimit: room});
      const assets = (result.assets ?? []).filter(a => a.uri).slice(0, room);
      if (assets.length) {
        onAdd(assets);
      }
    } catch {
      // Picker closed or camera unavailable: nothing to do.
    }
  }

  return (
    <View>
      <View style={styles.grid}>
        {stored.map(p => (
          <View key={p.id} style={styles.tile}>
            <Image source={photoSource(p.path)} style={styles.tileImage} />
            <Pressable
              style={styles.remove}
              onPress={() => onRemoveStored(p.id)}
              accessibilityLabel={t('photo.remove')}>
              <Text style={styles.removeText}>✕</Text>
            </Pressable>
          </View>
        ))}
        {added.map((a, i) => (
          <View key={a.uri} style={styles.tile}>
            <Image source={{uri: a.uri}} style={styles.tileImage} />
            <Pressable
              style={styles.remove}
              onPress={() => onRemoveAdded(i)}
              accessibilityLabel={t('photo.remove')}>
              <Text style={styles.removeText}>✕</Text>
            </Pressable>
          </View>
        ))}
        {room > 0 ? (
          <>
            <Pressable
              style={[styles.pickButton, styles.tile]}
              onPress={() => pick(true)}>
              <Glyph name="camera" size={22} />
              <Text style={styles.pickText}>{t('photo.camera')}</Text>
            </Pressable>
            <Pressable
              style={[styles.pickButton, styles.tile]}
              onPress={() => pick(false)}>
              <Glyph name="gallery" size={22} />
              <Text style={styles.pickText}>{t('photo.gallery')}</Text>
            </Pressable>
          </>
        ) : null}
      </View>
      {error ? <Text style={styles.error}>{error}</Text> : null}
    </View>
  );
}

// Stored photo thumbnail; tap for full screen. Loaded with the bearer token header.
export function StoredPhoto({path, large = false}) {
  const [open, setOpen] = useState(false);
  const [failed, setFailed] = useState(false);
  const {t} = useTranslation();
  const source = photoSource(path);

  if (failed) {
    return <Text style={styles.failed}>⚠ {t('errors.photo.notFound')}</Text>;
  }
  return (
    <>
      <Pressable
        onPress={() => setOpen(true)}
        accessibilityLabel={t('photo.view')}>
        <Image
          source={source}
          style={large ? styles.large : styles.thumb}
          onError={() => setFailed(true)}
        />
      </Pressable>
      <Modal
        visible={open}
        transparent
        animationType="fade"
        onRequestClose={() => setOpen(false)}>
        <Pressable style={styles.viewer} onPress={() => setOpen(false)}>
          <Image source={source} style={styles.full} resizeMode="contain" />
        </Pressable>
      </Modal>
    </>
  );
}

const styles = StyleSheet.create({
  row: {flexDirection: 'row', gap: 10},
  grid: {flexDirection: 'row', flexWrap: 'wrap', gap: 10},
  tile: {width: 84, height: 84},
  tileImage: {
    width: 84,
    height: 84,
    borderRadius: 12,
    backgroundColor: colors.creamDark,
  },
  pickButton: {
    width: 96,
    height: 84,
    borderRadius: 14,
    borderWidth: 2,
    borderStyle: 'dashed',
    borderColor: colors.creamDark,
    backgroundColor: colors.white,
    alignItems: 'center',
    justifyContent: 'center',
    gap: 4,
  },
  pickText: {fontSize: 12, color: colors.muted},
  previewWrap: {width: 112, height: 112},
  preview: {width: 112, height: 112, borderRadius: 14},
  remove: {
    position: 'absolute',
    top: -8,
    right: -8,
    width: 28,
    height: 28,
    borderRadius: 14,
    backgroundColor: colors.danger,
    alignItems: 'center',
    justifyContent: 'center',
  },
  removeText: {color: colors.white, fontWeight: '700'},
  error: {color: colors.danger, fontSize: 13, marginTop: 4},
  thumb: {
    width: 72,
    height: 72,
    borderRadius: 10,
    backgroundColor: colors.creamDark,
  },
  large: {
    width: '100%',
    height: 280,
    borderRadius: 14,
    backgroundColor: colors.creamDark,
  },
  failed: {fontSize: 12, color: colors.muted},
  viewer: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.9)',
    justifyContent: 'center',
  },
  full: {width: '100%', height: '100%'},
});
