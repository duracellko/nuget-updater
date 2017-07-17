using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGetUpdater
{
    public class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                var packageReferences = GetPackageReferences(args[0]);
                FindUpdates(packageReferences).Wait();

                Console.WriteLine();
                Console.WriteLine("Done");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private static Dictionary<string, int> GetTargetFrameworks()
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { ".NETFramework", 0 },
                { ".NETStandard", 10 },
                { "Any", 100 }
            };
        }

        private static Dictionary<string, string> GetPackageReferences(string projectFile)
        {
            var result = new Dictionary<string, string>();

            var projectDocument = XDocument.Load(projectFile);
            var packageReferenceElements = projectDocument.Element("Project").Elements("ItemGroup").SelectMany(e => e.Elements("PackageReference"));
            foreach (var packageReference in packageReferenceElements)
            {
                var packageId = (string)packageReference.Attribute("Include");
                var version = (string)packageReference.Attribute("Version");
                result.Add(packageId, version);
            }

            return result;
        }

        private static async Task FindUpdates(Dictionary<string, string> packageReferences)
        {
            var logger = new Logger();
            var packages = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase);

            List<Lazy<INuGetResourceProvider>> providers = new List<Lazy<INuGetResourceProvider>>();
            providers.AddRange(Repository.Provider.GetCoreV3());
            PackageSource packageSource = new PackageSource("https://api.nuget.org/v3/index.json");
            SourceRepository sourceRepository = new SourceRepository(packageSource, providers);
            PackageMetadataResource packageMetadataResource = await sourceRepository.GetResourceAsync<PackageMetadataResource>();

            foreach (var packageReference in packageReferences)
            {
                await GetDependencies(packageReference.Key, NuGetVersion.Parse(packageReference.Value), packages, packageMetadataResource, logger, GetTargetFrameworks());
            }


            var updates = await GetUpdates(packages, packageMetadataResource, logger);

            Console.WriteLine();
            Console.WriteLine("-------------------------");
            Console.WriteLine("--- Available updates ---");
            Console.WriteLine("-------------------------");
            Console.WriteLine();

            foreach (var update in updates.OrderBy(u => u.Item1))
            {
                Console.WriteLine("{0}: {1} -> {2}", update.Item1, update.Item2, update.Item3);
            }
        }

        private static async Task GetDependencies(string packageId, NuGetVersion version, Dictionary<string, NuGetVersion> packages, PackageMetadataResource resource, ILogger logger, Dictionary<string, int> frameworks)
        {
            NuGetVersion existingVersion = null;
            if (packages.TryGetValue(packageId, out existingVersion))
            {
                if (version > existingVersion)
                {
                    packages[packageId] = version;
                }
                else
                {
                    return;
                }
            }

            Console.WriteLine("Exploring package: {0} ({1})", packageId, version);

            var identity = new PackageIdentity(packageId, version);
            var packageMetadata = await resource.GetMetadataAsync(identity, logger, CancellationToken.None);

            if (packageMetadata != null)
            {
                packages[packageMetadata.Identity.Id] = packageMetadata.Identity.Version;

                if (packageMetadata.DependencySets.Any())
                {
                    var dependencySet = packageMetadata.DependencySets.Where(d => frameworks.ContainsKey(d.TargetFramework.Framework))
                        .OrderBy(d => frameworks[d.TargetFramework.Framework]).ThenByDescending(d => d.TargetFramework.Version).First();

                    foreach (var dependencyPackage in dependencySet.Packages)
                    {
                        await GetDependencies(dependencyPackage.Id, dependencyPackage.VersionRange.MinVersion, packages, resource, logger, frameworks);
                    }
                }
            }
            else
            {
                Console.WriteLine("[WARN] Package {0} ({1}) not found.", packageId, version);
                packages.Remove(packageId);
            }
        }

        private static async Task<IEnumerable<Tuple<string, NuGetVersion, NuGetVersion>>> GetUpdates(Dictionary<string, NuGetVersion> packages, PackageMetadataResource resource, ILogger logger)
        {
            var result = new List<Tuple<string, NuGetVersion, NuGetVersion>>();
            foreach (var item in packages)
            {
                Console.WriteLine("Checking update for: {0} ({1})", item.Key, item.Value);
                var packageMetadataList = await resource.GetMetadataAsync(item.Key, false, false, logger, CancellationToken.None);
                var packageMetadata = packageMetadataList.OrderByDescending(p => p.Identity.Version).First();

                if (packageMetadata.Identity.Version > item.Value)
                {
                    result.Add(Tuple.Create(packageMetadata.Identity.Id, item.Value, packageMetadata.Identity.Version));
                }
            }

            return result;
        }
    }
}