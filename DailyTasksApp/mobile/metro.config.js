const path = require('path');
const {getDefaultConfig, mergeConfig} = require('@react-native/metro-config');

// The translations are shared with the web app: one set of locale files for both clients.
const sharedLocales = path.resolve(__dirname, '../web/src/i18n/locales');

const config = {
  watchFolders: [sharedLocales],
};

module.exports = mergeConfig(getDefaultConfig(__dirname), config);
