using System.Collections.Generic;
using Task = System.Threading.Tasks.Task;
using Amazon.Lambda.Core;
using System.Threading.Tasks;

namespace BrickBridge.Lambda.VilCap
{
    public class LambdaMemoryStore : Google.Apis.Util.Store.IDataStore
    {
        private Dictionary<string, string> dictionary = new Dictionary<string, string>();
        public Task ClearAsync()
        {
            return Task.Run(() => dictionary.Clear());
        }

        public Task DeleteAsync<T>(string key)
        {
            return Task.Run(() => dictionary.Remove(key));
        }

        public Task<T> GetAsync<T>(string key)
        {
            return Task.Run<T>(() =>
            {
                var t = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(dictionary[key]);
                return t;
            });
        }

        public Task StoreAsync<T>(string key, T value)
        {
            
            System.Console.WriteLine($"Adding key: [{key}]");
            return Task.Run(() =>
            { 
                var t = Newtonsoft.Json.JsonConvert.SerializeObject(value);
                dictionary.Add(key, t);
            });
        }
    }
}
