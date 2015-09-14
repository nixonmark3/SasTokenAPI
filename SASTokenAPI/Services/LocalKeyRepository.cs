﻿using Newtonsoft.Json;
using Nito.AsyncEx;
using SASTokenAPI.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace SASTokenAPI.Services
{
    public class LocalKeyRepository : IKeyRepository
    {
        Dictionary<string, KeyRegistration> _keys;

        private readonly AsyncLock _mutex = new AsyncLock();

        public LocalKeyRepository()
        {
            LoadKeys();
        }

        void LoadKeys()
        {
            if (HttpContext.Current.Application["SasKeys"] != null)
            {
                _keys = HttpContext.Current.Application["SasKeys"] as Dictionary<string, KeyRegistration>;
                return;
            }

            var filePath = HttpContext.Current.Server.MapPath("~/sas.dat");
            if (File.Exists(filePath))
            {
                var file = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                var reader = new StreamReader(file);
                var json = reader.ReadToEnd();
                file.Close();
                file.Dispose();

                try
                {
                    _keys = JsonConvert.DeserializeObject<Dictionary<string, KeyRegistration>>(json);
                   
                }
                catch (Exception ex)
                {
                    _keys = null;
                }
            }
            else
                _keys = new Dictionary<string, KeyRegistration>() { { "testServiceNamespace:testEventHub:testSender",
                                                                  new KeyRegistration()
                                                                  {
                                                                      ServiceNamespace = "testServiceNamespace",
                                                                      EventHub = "testEventHub",
                                                                      KeyName = "testSender",
                                                                      KeyValue = "testkeyvalue"
                                                                  }
                                                                 } };

            HttpContext.Current.Application["SasKeys"] = _keys;
        }

        
        public async Task<IEnumerable<KeyRegistration>> GetRegistrationsAsync()
        {
            return _keys.Values;
        }


        public async Task<string> GetKeyAsync(string serviceNamespace, string eventHub, string keyName) {
            if (!(await ContainsKeyAsync(serviceNamespace, eventHub, keyName)))
                return null;

            var lookup = string.Format("{0}:{1}:{2}", serviceNamespace, eventHub, keyName);
            return (from k in _keys.Keys where k == lookup select k).First();

        }

        public async Task<string> GetKeyAsync(KeyRegistration registration)
        {
            return await GetKeyAsync(registration.ServiceNamespace, registration.EventHub, registration.KeyName);
        }


        public async Task SaveKeyAsync(string serviceNamespace, string eventHub, string keyName, string key)
        {
           
            var registration = new KeyRegistration()
            {
                ServiceNamespace = serviceNamespace,
                EventHub = eventHub,
                KeyName = keyName,
                KeyValue = key
            };
            await SaveKeyAsync(registration);
           
        }

        public async Task SaveKeyAsync(KeyRegistration registration)
        {
            string index = string.Format("{0}:{1}:{2}", registration.ServiceNamespace, registration.EventHub, registration.KeyName);
            if (_keys.ContainsKey(index))
                _keys[index] = registration;
            else
                _keys.Add(index, registration);

            await WriteKeysToDisk();
            HttpContext.Current.Application["SasKeys"] = _keys;
        }


        public async Task DeleteKeyAsync(string serviceNamespace, string eventHub, string keyName) {
            if (!(await ContainsKeyAsync(serviceNamespace, eventHub, keyName)))
                return;

            var lookup = string.Format("{0}:{1}:{2}", serviceNamespace, eventHub, keyName);
            _keys.Remove(lookup);
            await WriteKeysToDisk();
        }

        public async Task DeleteKeyAsync(KeyRegistration registration)
        {
            await DeleteKeyAsync(registration.ServiceNamespace, registration.EventHub, registration.KeyName);
        }

        public async Task<bool> ContainsKeyAsync(string serviceNamespace, string eventHub, string keyName) {
            var lookup = string.Format("{0}:{1}:{2}", serviceNamespace, eventHub, keyName);
            return (from k in _keys.Keys where k == lookup select k).Any();
        }

        public async Task<bool>  ContainsKeyAsync(KeyRegistration registration)
        {
            return await ContainsKeyAsync(registration.ServiceNamespace, registration.EventHub, registration.KeyName);
        }

        private async Task WriteKeysToDisk()
        {
            using (await _mutex.LockAsync())
            {

                try
                {
                    var json = JsonConvert.SerializeObject(_keys);
                    var file = new FileStream(HttpContext.Current.Server.MapPath("~/sas.dat"), FileMode.Create, FileAccess.Write);
                    var writer = new StreamWriter(file);
                    await writer.WriteLineAsync(json);
                    await writer.FlushAsync();
                    file.Close();
                    file.Dispose();
                }
                catch (Exception ex)
                {

                    throw;
                }
            }
        }
    }
}