using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Allow2
{
    public class Connection
    {
        private static readonly HttpClient client = new HttpClient();

        public Connection()
        {
        }

        public async Task<string> test()
        {

            //var values = new Dictionary<string, string>
            //    {
            //       { "thing1", "hello" },
            //       { "thing2", "world" }
            //    };

            //var content = new FormUrlEncodedContent(values);

            //var response = await client.PostAsync("http://api.allow2.com/", content);

            //var responseString = await response.Content.ReadAsStringAsync();

            var responseString = await client.GetStringAsync("http://api.allow2.com/");

            return responseString;
        }
    }
}
