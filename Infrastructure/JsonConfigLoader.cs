using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using MarcasZK.Domain;

namespace MarcasZK.Infrastructure
{
    /// <summary>
    /// Reads config.json from disk and maps it to Device[] domain entities.
    /// Uses JavaScriptSerializer (System.Web.Extensions) — no NuGet required.
    /// Throws a descriptive exception on any failure so Program.cs can exit(1) cleanly.
    /// </summary>
    public class JsonConfigLoader
    {
        public static Device[] Load(string configPath)
        {
            if (!File.Exists(configPath))
                throw new InvalidOperationException("config.json not found at: " + configPath);

            string json = File.ReadAllText(configPath);
            return LoadFromJson(json);
        }

        public static Device[] LoadFromJson(string json)
        {
            if (json == null)
                throw new ArgumentNullException("json");

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            Dictionary<string, object> root;

            try
            {
                root = serializer.Deserialize<Dictionary<string, object>>(json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("config.json parse error: " + ex.Message, ex);
            }

            if (root == null || !root.ContainsKey("Devices"))
                throw new InvalidOperationException("config.json is missing the required 'Devices' array.");

            System.Collections.ArrayList devicesRaw = root["Devices"] as System.Collections.ArrayList;
            if (devicesRaw == null)
                throw new InvalidOperationException("config.json 'Devices' must be a JSON array.");

            List<Device> devices = new List<Device>();

            foreach (object item in devicesRaw)
            {
                Dictionary<string, object> entry = item as Dictionary<string, object>;
                if (entry == null)
                    throw new InvalidOperationException("Each entry in 'Devices' must be a JSON object.");

                string ip = RequireString(entry, "ip");
                int port = RequireInt(entry, "port");
                string password = GetString(entry, "password"); // optional — empty string if absent
                string origen = RequireString(entry, "origen");
                string nombre = RequireString(entry, "nombre");
                bool borrarMarcas = GetBool(entry, "borrarMarcas");

                devices.Add(new Device
                {
                    Origen = origen,
                    Nombre = nombre,
                    BorrarMarcas = borrarMarcas,
                    Connection = new DeviceConnection
                    {
                        Ip = ip,
                        Port = port,
                        Password = password
                    }
                });
            }

            return devices.ToArray();
        }

        private static string RequireString(Dictionary<string, object> entry, string key)
        {
            if (!entry.ContainsKey(key))
                throw new InvalidOperationException(
                    string.Format("A device entry is missing the required field '{0}'.", key));

            object val = entry[key];
            if (val == null)
                throw new InvalidOperationException(
                    string.Format("Device field '{0}' must not be null.", key));

            return val.ToString();
        }

        private static int RequireInt(Dictionary<string, object> entry, string key)
        {
            if (!entry.ContainsKey(key))
                throw new InvalidOperationException(
                    string.Format("A device entry is missing the required field '{0}'.", key));

            object val = entry[key];
            if (val == null)
                throw new InvalidOperationException(
                    string.Format("Device field '{0}' must not be null.", key));

            try
            {
                return Convert.ToInt32(val);
            }
            catch
            {
                throw new InvalidOperationException(
                    string.Format("Device field '{0}' must be an integer. Got: {1}", key, val));
            }
        }

        private static string GetString(Dictionary<string, object> entry, string key)
        {
            if (!entry.ContainsKey(key) || entry[key] == null)
                return string.Empty;

            return entry[key].ToString();
        }

        private static bool GetBool(Dictionary<string, object> entry, string key)
        {
            if (!entry.ContainsKey(key) || entry[key] == null)
                return false;

            object val = entry[key];
            if (val is bool)
                return (bool)val;

            bool result;
            if (bool.TryParse(val.ToString(), out result))
                return result;

            return false;
        }
    }
}
