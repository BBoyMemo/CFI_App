import { chromium } from 'playwright';

(async () => {
  const browser = await chromium.launch();
  const context = await browser.createBrowserContext();
  const page = await context.newPage();
  
  try {
    await page.goto('http://localhost:5173/', { waitUntil: 'networkidle' });
    await page.screenshot({ path: 'screenshot.png', fullPage: true });
    console.log('Screenshot saved: screenshot.png');
    
    // Get page title and some info
    const title = await page.title();
    console.log('Page Title:', title);
    
    // Wait a moment and take another screenshot
    await page.waitForTimeout(2000);
    const bodyText = await page.textContent('body');
    console.log('Body content (first 500 chars):', bodyText ? bodyText.substring(0, 500) : 'No content');
  } catch (error) {
    console.error('Error:', error.message);
  } finally {
    await browser.close();
  }
})();
