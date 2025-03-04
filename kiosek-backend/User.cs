using System;
using System.Threading.Tasks;
using PuppeteerSharp;

public class User
{
    public string Name { get; set; }
    public string Username { get; set; }
    public string Age { get; set; }
    public string Born { get; set; }
    public string Tel { get; set; }
    public string Address { get; set; }
    public string Class { get; set; }
    public string Id { get; set; }
    public string Email { get; set; }
    public string SchoolEmail { get; set; }
    public string ImageUrl { get; set; }
}

public class UserDataFetcher
{
    public static async Task<User> GetUserDataAsync(string username, string password)
    {
        await new BrowserFetcher().DownloadAsync();
        await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true });
        await using var page = await browser.NewPageAsync();

        try
        {
            await page.GoToAsync($"https://www.spsejecna.cz/student/{username}", new NavigationOptions { WaitUntil = new[] { WaitUntilNavigation.Networkidle2 } });
            
            await page.TypeAsync("#user", username);
            await page.TypeAsync("#pass", password);
            await Task.WhenAll(
                page.WaitForNavigationAsync(new NavigationOptions { WaitUntil = new[] { WaitUntilNavigation.Networkidle2 } }),
                page.ClickAsync("#submit")
            );

            try
            {
                await page.WaitForSelectorAsync("table.userprofile", new WaitForSelectorOptions { Timeout = 5000 });
                await page.WaitForSelectorAsync("div.profilephoto img", new WaitForSelectorOptions { Timeout = 5000 });
            }
            catch (Exception)
            {
                return null;
            }
            
            var imageUrl = await page.EvaluateFunctionAsync<string>(@"() => {
                const imgElement = document.querySelector('div.profilephoto img');
                return imgElement ? imgElement.src : null;
            }");

            var user = await page.EvaluateFunctionAsync<User>(@"(imageUrl) => {
                const table = document.querySelector('table.userprofile');
                const rows = table ? table.querySelectorAll('tr') : [];
                const data = [];

                rows.forEach(row => {
                    const value = row.querySelector('td .value')?.textContent?.trim() || 
                                  row.querySelector('td a')?.textContent?.trim() || null;
                    data.push(value);
                });

                return {
                    name: data[0] || null,
                    username: data[1] || null,
                    age: data[2] || null,
                    born: data[3] || null,
                    tel: data[4] || null,
                    address: data[5] || null,
                    class: data[6]?.split(',')[0] || null,
                    id: data[7] || null,
                    email: data[8] || null,
                    school_email: data[9] || null,
                    image_url: imageUrl ? new URL(imageUrl, window.location.href).href : null,
                };
            }", imageUrl);
            
            return user;
        }
        catch (Exception ex)
        {
            Console.WriteLine("An error occurred: " + ex.Message);
            return null;
        }
    }
}
