import puppeteer from 'puppeteer';

export interface User {
  name: string;
  username: string;
  age: string;
  born: string;
  tel: string;
  address: string;
  class: string;
  id: string;
  email: string;
  schoolEmail: string;
  imageUrl: string;
}

export class UserDataFetcher {
  static async getUserDataAsync(username: string, password: string): Promise<User | null> {
    // Use the newer Puppeteer API without BrowserFetcher
    const browser = await puppeteer.launch({ headless: 'new' });
    const page = await browser.newPage();

    try {
      await page.goto(`https://www.spsejecna.cz/student/${username}`, { 
        waitUntil: 'networkidle2' 
      });
      
      await page.type('#user', username);
      await page.type('#pass', password);
      
      await Promise.all([
        page.waitForNavigation({ waitUntil: 'networkidle2' }),
        page.click('#submit')
      ]);

      try {
        await page.waitForSelector('table.userprofile', { timeout: 5000 });
        await page.waitForSelector('div.profilephoto img', { timeout: 5000 });
      } catch (error) {
        await browser.close();
        return null;
      }
      
      const imageUrl = await page.evaluate(() => {
        const imgElement = document.querySelector('div.profilephoto img') as HTMLImageElement;
        return imgElement ? imgElement.src : null;
      });

      const user = await page.evaluate((imageUrl: string | null) => {
        const table = document.querySelector('table.userprofile');
        const rows = table ? table.querySelectorAll('tr') : [];
        const data: string[] = [];

        rows.forEach(row => {
          const value = row.querySelector('td .value')?.textContent?.trim() || 
                        row.querySelector('td a')?.textContent?.trim() || null;
          if (value) data.push(value);
        });

        return {
          name: data[0] || "",
          username: data[1] || "",
          age: data[2] || "",
          born: data[3] || "",
          tel: data[4] || "",
          address: data[5] || "",
          class: data[6]?.split(',')[0] || "",
          id: data[7] || "",
          email: data[8] || "",
          schoolEmail: data[9] || "",
          imageUrl: imageUrl ? new URL(imageUrl, window.location.href).href : "",
        };
      }, imageUrl);
      
      await browser.close();
      return user;
    } catch (ex: any) {
      console.error("An error occurred:", ex.message);
      await browser.close();
      return null;
    }
  }
} 