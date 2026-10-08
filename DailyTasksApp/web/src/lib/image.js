const MAX_SIDE = 1920;
const QUALITY = 0.85;

// Phone photos are often 4–12 MB. Shrinking them before upload keeps uploads fast on factory
// wifi and well under the server's 10 MB limit. If the browser cannot decode the file
// (e.g. HEIC on some browsers) the original is sent and the server decides.
export async function shrinkPhoto(file) {
  if (!file.type.startsWith('image/')) return file;
  try {
    const bitmap = await createImageBitmap(file);
    const scale = Math.min(1, MAX_SIDE / Math.max(bitmap.width, bitmap.height));
    if (scale === 1 && file.size < 2 * 1024 * 1024 && file.type !== 'image/heic') {
      bitmap.close();
      return file;
    }
    const canvas = document.createElement('canvas');
    canvas.width = Math.round(bitmap.width * scale);
    canvas.height = Math.round(bitmap.height * scale);
    canvas.getContext('2d').drawImage(bitmap, 0, 0, canvas.width, canvas.height);
    bitmap.close();
    const blob = await new Promise((resolve) => canvas.toBlob(resolve, 'image/jpeg', QUALITY));
    if (!blob) return file;
    return new File([blob], file.name.replace(/\.\w+$/, '') + '.jpg', { type: 'image/jpeg' });
  } catch {
    return file;
  }
}
