// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.Functions.SdkE2ETests
{
    public class PublishTests
    {
        private ITestOutputHelper _testOutputHelper;

        public PublishTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public async Task Publish()
        {
            string outputDir = await TestUtility.InitializeTestAsync(_testOutputHelper, nameof(Publish));

            // Name of the csproj
            string projectNameToTest = "FunctionApp";
            string projectFileDirectory = Path.Combine(TestUtility.SamplesRoot, projectNameToTest);

            // Restore
            string dotnetArgs = $"restore {projectNameToTest}.csproj --source {TestUtility.LocalPackages}";
            int? exitCode = await new ProcessWrapper().RunProcess(TestUtility.DotNetExecutable, dotnetArgs, projectFileDirectory, testOutputHelper: _testOutputHelper);
            Assert.True(exitCode.HasValue && exitCode.Value == 0);

            // Publish
            dotnetArgs = $"publish {projectNameToTest}.csproj --configuration {TestUtility.Configuration} -o {outputDir}";
            exitCode = await new ProcessWrapper().RunProcess(TestUtility.DotNetExecutable, dotnetArgs, projectFileDirectory, testOutputHelper: _testOutputHelper);
            Assert.True(exitCode.HasValue && exitCode.Value == 0);

            // Make sure files are in /bin
            string additionalBinDir = Path.Combine(outputDir, "bin");
            Assert.True(Directory.Exists(additionalBinDir));
            var files = Directory.EnumerateFiles(additionalBinDir);

            // Verify files are present
            string metadataLoaderPath = Path.Combine(additionalBinDir, "Microsoft.Azure.WebJobs.Extensions.FunctionMetadataLoader.dll");
            string extensionsJsonPath = Path.Combine(additionalBinDir, "extensions.json");
            string functionsMetadataPath = Path.Combine(outputDir, "functions.metadata");
            Assert.True(File.Exists(metadataLoaderPath));
            Assert.True(File.Exists(extensionsJsonPath));
            Assert.True(File.Exists(functionsMetadataPath));

            // Verify extensions.json
            JObject jObjects = JObject.Parse(File.ReadAllText(extensionsJsonPath));
            JObject extensionsJsonContents = jObjects;
            JToken expected = JObject.FromObject(new
            {
                extensions = new[]
                {
                    new Extension("Startup", "Microsoft.Azure.WebJobs.Extensions.FunctionMetadataLoader.Startup, Microsoft.Azure.WebJobs.Extensions.FunctionMetadataLoader, Version=1.0.0.0, Culture=neutral, PublicKeyToken=551316b6919f366c"),
                    new Extension("AzureStorage", "Microsoft.Azure.WebJobs.Extensions.Storage.AzureStorageWebJobsStartup, Microsoft.Azure.WebJobs.Extensions.Storage, Version=4.0.3.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35")
                }
            });
            Assert.True(JToken.DeepEquals(extensionsJsonContents, expected), $"Actual: {extensionsJsonContents}{Environment.NewLine}Expected: {expected}");

            // Verify functions.metadata
            TestUtility.ValidateFunctionsMetadata(functionsMetadataPath, "functions.metadata");
        }

        private class Extension
        {
            public Extension(string name, string typeName)
            {
                Name = name;
                TypeName = typeName;
            }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("typeName")]
            public string TypeName { get; set; }
        }
    }
}