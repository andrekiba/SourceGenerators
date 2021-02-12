using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Generators
{
    public class ResourceReader
    {
        public static string GetResource<TAssembly>(string endsWith)
        {
            return GetResource(endsWith, typeof(TAssembly));
        }

        public static string GetResource(string endsWith, Type assemblyType = null)
        {
            var assembly = GetAssembly(assemblyType);

            var resources = assembly.GetManifestResourceNames()
                .Where(r => r.EndsWith(endsWith))
                .ToList();

            if (!resources.Any())
                throw new InvalidOperationException($"There is no resource that ends with '{endsWith}'");
            if (resources.Count > 1)
                throw new InvalidOperationException($"There is more then one resource that ends with '{endsWith}'");

            var rName = resources.Single();

            return ReadEmbededResource(assembly, rName);
        }

        static Assembly GetAssembly(Type assemblyType)
        {
            var assembly = assemblyType == null ? 
                Assembly.GetExecutingAssembly() : 
                Assembly.GetAssembly(assemblyType);

            return assembly;
        }
        static string ReadEmbededResource(Assembly assembly, string name)
        {
            using var resourceStream = assembly.GetManifestResourceStream(name);
            if (resourceStream == null) return null;
            using var streamReader = new StreamReader(resourceStream);
            return streamReader.ReadToEnd();
        }
    }
}