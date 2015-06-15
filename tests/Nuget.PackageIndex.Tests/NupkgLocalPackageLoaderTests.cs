﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Threading;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Runtime.Versioning;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nuget.PackageIndex.Abstractions;
using NuGet;
using Moq;
using IFileSystem = Nuget.PackageIndex.Abstractions.IFileSystem;

namespace Nuget.PackageIndex.Tests
{
    [TestClass]
    public class NupkgLocalPackageLoaderTests
    {
        [TestMethod]
        public void NupkgLocalPackageLoader_GetPackages_NoSourceDirsExist()
        {
            // Arrange
            var indexedPackages = new HashSet<string>();
            var sourceDirectories = new List<string>();
            var mockFileSystem = new Mock<IFileSystem>(MockBehavior.Strict);

            // Act           
            var loader = new NupkgLocalPackageLoader(mockFileSystem.Object, null);
            var returnedPackages = loader.GetPackages(sourceDirectories, 
                                                      indexedPackages, 
                                                      newOnly:false, 
                                                      lastIndexModifiedTime:DateTime.Now,
                                                      cancellationToken:CancellationToken.None);
            // Assert
            Assert.IsFalse(returnedPackages.Any());
            mockFileSystem.VerifyAll();

            // Arrange 
            sourceDirectories = new List<string> {
                @"d:\somefolder\SomeSubFolder",
                @"c:\Program Files\SomeSubFolder"
            };
            foreach(var dir in sourceDirectories)
            {
                mockFileSystem.Setup(x => x.DirectoryExists(dir)).Returns(false);
            }

            // Act
            returnedPackages = loader.GetPackages(sourceDirectories,
                                                      indexedPackages,
                                                      newOnly: false,
                                                      lastIndexModifiedTime: DateTime.Now,
                                                      cancellationToken: CancellationToken.None);

            // Assert
            Assert.IsFalse(returnedPackages.Any());
            mockFileSystem.VerifyAll();
        }

        [TestMethod]
        public void NupkgLocalPackageLoader_GetPackages_NewOnly()
        {
            // Arrange
            var indexedPackages = new HashSet<string>();
            var sourceDirectories = new Dictionary<string, string[]> {
                { @"d:\somefolder\SomeSubFolder", new [] { @"d:\somefolder\SomeSubFolder\dpackage1.nupkg", @"d:\somefolder\SomeSubFolder\dpackage2.nupkg"} },
                { @"c:\Program Files\SomeSubFolder", new [] { @"c:\Program Files\SomeSubFolder\cpackage1.nupkg", @"c:\Program Files\SomeSubFolder\cpackage2.nupkg" } }
            };

            var mockFileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
            foreach (var dirKvp in sourceDirectories)
            {
                mockFileSystem.Setup(x => x.DirectoryExists(dirKvp.Key)).Returns(true);
                mockFileSystem.Setup(x => x.DirectoryGetFiles(dirKvp.Key, "*.nupkg", SearchOption.AllDirectories)).Returns(dirKvp.Value);
            }

            mockFileSystem.Setup(x => x.FileGetLastWriteTime(@"d:\somefolder\SomeSubFolder\dpackage1.nupkg")).Returns(DateTime.Now.AddDays(1));
            mockFileSystem.Setup(x => x.FileGetLastWriteTime(@"d:\somefolder\SomeSubFolder\dpackage2.nupkg")).Returns(DateTime.Now.AddDays(-1));
            mockFileSystem.Setup(x => x.FileGetLastWriteTime(@"c:\Program Files\SomeSubFolder\cpackage1.nupkg")).Returns(DateTime.Now.AddDays(-1));
            mockFileSystem.Setup(x => x.FileGetLastWriteTime(@"c:\Program Files\SomeSubFolder\cpackage2.nupkg")).Returns(DateTime.Now.AddDays(1));

            // Act           
            var loader = new NupkgLocalPackageLoader(mockFileSystem.Object, null);
            var returnedPackages = loader.GetPackages(sourceDirectories.Keys,
                                                      indexedPackages,
                                                      newOnly: true,
                                                      lastIndexModifiedTime: DateTime.Now,
                                                      cancellationToken: CancellationToken.None).ToList();
            // Assert
            Assert.AreEqual(2, returnedPackages.Count());
            Assert.AreEqual(@"d:\somefolder\SomeSubFolder\dpackage1.nupkg", returnedPackages[0]);
            Assert.AreEqual(@"c:\Program Files\SomeSubFolder\cpackage2.nupkg", returnedPackages[1]);
            mockFileSystem.VerifyAll();
        }

        [TestMethod]
        public void NupkgLocalPackageLoader_GetPackages_AlreadyIndexed()
        {
            // Arrange
            var indexedPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            indexedPackages.Add(@"c:\Program Files\SomeSubFolder\cpackage1.nupkg");
            var sourceDirectories = new Dictionary<string, string[]> {
                { @"d:\somefolder\SomeSubFolder", new [] { @"d:\somefolder\SomeSubFolder\dpackage1.nupkg", @"d:\somefolder\SomeSubFolder\dpackage2.nupkg"} },
                { @"c:\Program Files\SomeSubFolder", new [] { @"c:\Program Files\SomeSubFolder\cpackage1.nupkg", @"c:\Program Files\SomeSubFolder\cpackage2.nupkg" } }
            };

            var mockFileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
            foreach (var dirKvp in sourceDirectories)
            {
                mockFileSystem.Setup(x => x.DirectoryExists(dirKvp.Key)).Returns(true);
                mockFileSystem.Setup(x => x.DirectoryGetFiles(dirKvp.Key, "*.nupkg", SearchOption.AllDirectories)).Returns(dirKvp.Value);
            }


            // Act           
            var loader = new NupkgLocalPackageLoader(mockFileSystem.Object, null);
            var returnedPackages = loader.GetPackages(sourceDirectories.Keys,
                                                      indexedPackages,
                                                      newOnly: false,
                                                      lastIndexModifiedTime: DateTime.Now,
                                                      cancellationToken: CancellationToken.None);
            // Assert
            Assert.AreEqual(3, returnedPackages.Count());
            mockFileSystem.VerifyAll();
        }

        [TestMethod]
        public void NupkgLocalPackageLoader_GetPackageMetadataFromPath_NupkgDoesNotExist()
        {
            // Arrange
            var mockFileSystem = new Mock<IFileSystem>(MockBehavior.Strict);

            // Act           
            var loader = new NupkgLocalPackageLoader(mockFileSystem.Object, null);
            var returnedPackageMetadata = loader.GetPackageMetadataFromPath(null);

            // Assert
            Assert.IsNull(returnedPackageMetadata);
            mockFileSystem.VerifyAll();

            returnedPackageMetadata = loader.GetPackageMetadataFromPath(string.Empty);

            // Assert
            Assert.IsNull(returnedPackageMetadata);
            mockFileSystem.VerifyAll();

            // Arrange
            mockFileSystem.Setup(x => x.FileExists(@"d:\myfolder\sample.nupkg")).Returns(false);

            // Act           
            returnedPackageMetadata = loader.GetPackageMetadataFromPath(@"d:\myfolder\sample.nupkg");

            // Assert
            Assert.IsNull(returnedPackageMetadata);
            mockFileSystem.VerifyAll();
        }

        [TestMethod]
        public void NupkgLocalPackageLoader_GetPackageMetadataFromPath_LibDllUnderSameFolderAsNupkg()
        {
            // Arrange 
            var mock = new MockGenerator()
                           .MockOpenExistingPackage()
                           .MockPackageInfo()
                           .MockLibDllInTheSameFolderAsNupkg();

            PackageMetadata expectedPackageMetadata = mock.GetExpectedPackageMetadata();

            // Act
            var loader = new NupkgLocalPackageLoader(mock.FileSystem.Object, mock.NugetHelper.Object);
            var returnedPackageMetadata = loader.GetPackageMetadataFromPath(mock.NupkgFile);

            // Assert
            Assert.IsTrue(expectedPackageMetadata.Equals(returnedPackageMetadata));
            mock.VerifyAll();
        }

        [TestMethod]
        public void NupkgLocalPackageLoader_GetPackageMetadataFromPath_LibDllUnderRelativeFolderAsNupkg()
        {
            // Arrange 
            var mock = new MockGenerator()
                           .MockOpenExistingPackage()
                           .MockPackageInfo()
                           .MockLibDllUnderRelativeFolderAsNupkg();
            
            PackageMetadata expectedPackageMetadata = mock.GetExpectedPackageMetadata();

            // Act
            var loader = new NupkgLocalPackageLoader(mock.FileSystem.Object, mock.NugetHelper.Object);
            var returnedPackageMetadata = loader.GetPackageMetadataFromPath(mock.NupkgFile);

            // Assert
            Assert.IsTrue(expectedPackageMetadata.Equals(returnedPackageMetadata));
            mock.VerifyAll();
        }

        [TestMethod]
        public void NupkgLocalPackageLoader_GetPackageMetadataFromPath_RefAny()
        {
            // Arrange 
            var mock = new MockGenerator()
                           .MockOpenExistingPackage()
                           .MockPackageInfo()
                           .MockRefAnyDll();

            PackageMetadata expectedPackageMetadata = mock.GetExpectedPackageMetadata();

            // Act
            var loader = new NupkgLocalPackageLoader(mock.FileSystem.Object, mock.NugetHelper.Object);
            var returnedPackageMetadata = loader.GetPackageMetadataFromPath(mock.NupkgFile);

            // Assert
            Assert.IsTrue(expectedPackageMetadata.Equals(returnedPackageMetadata));
            mock.VerifyAll();
        }

        [TestMethod]
        public void NupkgLocalPackageLoader_GetPackageMetadataFromPath_RefFx()
        {
            // Arrange 
            var mock = new MockGenerator()
                           .MockOpenExistingPackage()
                           .MockPackageInfo()
                           .MockRefFxDll();

            PackageMetadata expectedPackageMetadata = mock.GetExpectedPackageMetadata();

            // Act
            var loader = new NupkgLocalPackageLoader(mock.FileSystem.Object, mock.NugetHelper.Object);
            var returnedPackageMetadata = loader.GetPackageMetadataFromPath(mock.NupkgFile);

            // Assert
            Assert.IsTrue(expectedPackageMetadata.Equals(returnedPackageMetadata));
            mock.VerifyAll();
        }

        [TestMethod]
        public void NupkgLocalPackageLoader_GetPackageMetadataFromPath_LibToolsAndContent()
        {
            // Arrange 
            var mock = new MockGenerator()
                           .MockOpenExistingPackage()
                           .MockPackageInfo()
                           .MockToolsAndContentDlls();

            PackageMetadata expectedPackageMetadata = mock.GetExpectedPackageMetadata();

            // Act
            var loader = new NupkgLocalPackageLoader(mock.FileSystem.Object, mock.NugetHelper.Object);
            var returnedPackageMetadata = loader.GetPackageMetadataFromPath(mock.NupkgFile);

            // Assert
            Assert.IsTrue(expectedPackageMetadata.Equals(returnedPackageMetadata));
            mock.VerifyAll();
        }

        [TestMethod]
        public void NupkgLocalPackageLoader_GetPackageMetadataFromPath_LibFx()
        {
            // Arrange 
            var mock = new MockGenerator()
                           .MockOpenExistingPackage()
                           .MockPackageInfo()
                           .MockLibFxDll();

            PackageMetadata expectedPackageMetadata = mock.GetExpectedPackageMetadata();

            // Act
            var loader = new NupkgLocalPackageLoader(mock.FileSystem.Object, mock.NugetHelper.Object);
            var returnedPackageMetadata = loader.GetPackageMetadataFromPath(mock.NupkgFile);

            // Assert
            Assert.IsTrue(expectedPackageMetadata.Equals(returnedPackageMetadata));
            mock.VerifyAll();
        }

        [TestMethod]
        public void NupkgLocalPackageLoader_DiscoverPackages_ValidPackage()
        {
            // Arrange 
            var mock = new MockGenerator()
                           .MockGetPackages()
                           .MockOpenExistingPackage()
                           .MockPackageInfo()
                           .MockLibDllInTheSameFolderAsNupkg();

            PackageMetadata expectedPackageMetadata = mock.GetExpectedPackageMetadata();

            // Act
            var loader = new NupkgLocalPackageLoader(mock.FileSystem.Object, mock.NugetHelper.Object);
            var returnedPackageMetadata = loader.DiscoverPackages(new [] { mock.SourceDirectory },
                                                                 new HashSet<string>(),
                                                                 newOnly: false,
                                                                 lastIndexModifiedTime: DateTime.Now,
                                                                 cancellationToken: CancellationToken.None);

            // Assert
            Assert.IsTrue(expectedPackageMetadata.Equals(returnedPackageMetadata.First()));
            mock.VerifyAll();
        }

        [TestMethod]
        public void NupkgLocalPackageLoader_DiscoverPackages_NupkgNotFound()
        {
            // Arrange 
            var mock = new MockGenerator()
                           .MockGetPackages()
                           .MockNupkgNotFound();

            // Act
            var loader = new NupkgLocalPackageLoader(mock.FileSystem.Object, mock.NugetHelper.Object);
            var returnedPackageMetadata = loader.DiscoverPackages(new[] { mock.SourceDirectory },
                                                                 new HashSet<string>(),
                                                                 newOnly: false,
                                                                 lastIndexModifiedTime: DateTime.Now,
                                                                 cancellationToken: CancellationToken.None);

            // Assert
            Assert.IsFalse(returnedPackageMetadata.Any());
            mock.VerifyAll();
        }

        private class MockGenerator
        {
            public Mock<IFileSystem> FileSystem { get; private set; }
            public Mock<INugetHelper> NugetHelper { get; private set; }
            private List<Mock<IPackageFile>> PackageFiles { get; set; }
            private List<AssemblyMetadata> PackageAssemblyMetadata { get; set; }

            public MockGenerator()
            {
                FileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
                NugetHelper = new Mock<INugetHelper>(MockBehavior.Strict);
                PackageFiles = new List<Mock<IPackageFile>>();
                PackageAssemblyMetadata = new List<AssemblyMetadata>();
            }

            public void VerifyAll()
            {
                if (FileSystem != null)
                {
                    FileSystem.VerifyAll();
                }

                if (NugetHelper != null)
                {
                    NugetHelper.VerifyAll();
                }

                foreach(var packageFile in PackageFiles)
                {
                    packageFile.VerifyAll();
                }
            }
            
            public string NupkgFile = @"d:\myfolder\sampleBar.nupkg";
            public List<FrameworkName> PackageFrameworks = new List<FrameworkName> {
                    VersionUtility.ParseFrameworkName("net45"),
                    VersionUtility.ParseFrameworkName("dnx451"),
                    VersionUtility.ParseFrameworkName("dnxcore451")
                };

            public MockGenerator MockOpenExistingPackage()
            {
                var stream = new MemoryStream();
                FileSystem.Setup(x => x.FileExists(NupkgFile)).Returns(true);
                FileSystem.Setup(x => x.FileOpenRead(NupkgFile)).Returns(stream);

                NugetHelper.Setup(x => x.OpenPackage(It.IsAny<Stream>()));

                return this;
            }

            public MockGenerator MockLibDllInTheSameFolderAsNupkg()
            {
                var mockPackageFile = new Mock<IPackageFile>(MockBehavior.Strict);
                mockPackageFile.Setup(x => x.EffectivePath).Returns("mySample.dLl");
                mockPackageFile.Setup(x => x.Path).Returns(@"lib\net45\mySample.dLl");
                mockPackageFile.Setup(x => x.SupportedFrameworks).Returns(new List<FrameworkName>() {                     
                            VersionUtility.ParseFrameworkName("net45")
                        });

                PackageFiles.Add(mockPackageFile);

                FileSystem.Setup(x => x.FileExists(Path.Combine(Path.GetDirectoryName(NupkgFile), @"lib\net45\mySample.dLl"))).Returns(true);

                var packageFiles = new List<IPackageFile>
                {
                    mockPackageFile.Object
                };

                var assemblyMetadata = new AssemblyMetadata
                {
                    FullPath = Path.Combine(Path.GetDirectoryName(NupkgFile), @"lib\net45\mySample.dLl"),
                    TargetFrameworks = new[] { "net45" }
                };

                PackageAssemblyMetadata.Add(assemblyMetadata);

                return this;
            }

            public MockGenerator MockLibDllUnderRelativeFolderAsNupkg()
            {
                var mockPackageFile = new Mock<IPackageFile>(MockBehavior.Strict);
                mockPackageFile.Setup(x => x.EffectivePath).Returns("myRelSample.dLl");
                mockPackageFile.Setup(x => x.Path).Returns(@"lib\net45\myRelSample.dLl");
                mockPackageFile.Setup(x => x.SupportedFrameworks).Returns(new List<FrameworkName>() {                     
                            VersionUtility.ParseFrameworkName("net45")
                        });

                PackageFiles.Add(mockPackageFile);

                FileSystem.Setup(x => x.FileExists(Path.Combine(Path.GetDirectoryName(NupkgFile), 
                                                                @"lib\net45\myRelSample.dLl"))).Returns(false);
                FileSystem.Setup(x => x.FileExists(Path.Combine(Path.GetDirectoryName(NupkgFile),
                                                                Path.GetFileNameWithoutExtension(NupkgFile),
                                                                @"lib\net45\myRelSample.dLl"))).Returns(true);

                var packageFiles = new List<IPackageFile>
                {
                    mockPackageFile.Object
                };

                var assemblyMetadata = new AssemblyMetadata
                {
                    FullPath = Path.Combine(Path.GetDirectoryName(NupkgFile), 
                                            Path.GetFileNameWithoutExtension(NupkgFile), 
                                            @"lib\net45\myRelSample.dLl"),
                    TargetFrameworks = new[] { "net45" }
                };

                PackageAssemblyMetadata.Add(assemblyMetadata);

                return this;
            }

            public MockGenerator MockRefAnyDll()
            {
                var mockPackageFile = new Mock<IPackageFile>(MockBehavior.Strict);
                mockPackageFile.Setup(x => x.EffectivePath).Returns("myAnySample.dLl");
                mockPackageFile.Setup(x => x.Path).Returns(@"ref\ANy\myAnySample.dLl");
                mockPackageFile.Setup(x => x.SupportedFrameworks).Returns(new List<FrameworkName>());

                var mockPackageFileLib = new Mock<IPackageFile>(MockBehavior.Strict);
                mockPackageFileLib.Setup(x => x.EffectivePath).Returns("myLibNoop.dLl");
                mockPackageFileLib.Setup(x => x.Path).Returns(@"lib\net45\myLibNoop.dLl");

                PackageFiles.Add(mockPackageFile);
                PackageFiles.Add(mockPackageFileLib);

                FileSystem.Setup(x => x.FileExists(Path.Combine(Path.GetDirectoryName(NupkgFile),
                                                                @"ref\ANy\myAnySample.dLl"))).Returns(true);
                var packageFiles = new List<IPackageFile>
                {
                    mockPackageFile.Object,
                    mockPackageFileLib.Object
                };

                var assemblyMetadata = new AssemblyMetadata
                {
                    FullPath = Path.Combine(Path.GetDirectoryName(NupkgFile),
                                            @"ref\ANy\myAnySample.dLl"),
                    TargetFrameworks = PackageFrameworks.Select(x => VersionUtility.GetShortFrameworkName(x))
                };

                PackageAssemblyMetadata.Add(assemblyMetadata);

                return this;
            }

            public MockGenerator MockToolsAndContentDlls()
            {
                var mockPackageFileTools = new Mock<IPackageFile>(MockBehavior.Strict);
                mockPackageFileTools.Setup(x => x.EffectivePath).Returns("myToolsSample.dLl");
                mockPackageFileTools.Setup(x => x.Path).Returns(@"lib\tOols\myToolsSample.dLl");

                var mockPackageFileContent = new Mock<IPackageFile>(MockBehavior.Strict);
                mockPackageFileContent.Setup(x => x.EffectivePath).Returns("myToolsSample.dLl");
                mockPackageFileContent.Setup(x => x.Path).Returns(@"lib\contenT\myToolsSample.dLl");

                PackageFiles.Add(mockPackageFileTools);
                PackageFiles.Add(mockPackageFileContent);

                var packageFiles = new List<IPackageFile>
                {
                    mockPackageFileTools.Object,
                    mockPackageFileContent.Object
                };

                return this;
            }

            public MockGenerator MockRefFxDll()
            {
                var mockPackageFile = new Mock<IPackageFile>(MockBehavior.Strict);
                mockPackageFile.Setup(x => x.EffectivePath).Returns("myRefFxSample.dLl");
                mockPackageFile.Setup(x => x.Path).Returns(@"ref\dnx451\myRefFxSample.dLl");
                mockPackageFile.Setup(x => x.SupportedFrameworks).Returns(new List<FrameworkName>());

                PackageFiles.Add(mockPackageFile);

                FileSystem.Setup(x => x.FileExists(Path.Combine(Path.GetDirectoryName(NupkgFile),
                                                                @"ref\dnx451\myRefFxSample.dLl"))).Returns(true);
                var packageFiles = new List<IPackageFile>
                {
                    mockPackageFile.Object
                };

                var assemblyMetadata = new AssemblyMetadata
                {
                    FullPath = Path.Combine(Path.GetDirectoryName(NupkgFile),
                                            @"ref\dnx451\myRefFxSample.dLl"),
                    TargetFrameworks = new [] { "dnx451" }
                };

                PackageAssemblyMetadata.Add(assemblyMetadata);

                return this;
            }

            public MockGenerator MockLibFxDll()
            {
                var mockPackageFile = new Mock<IPackageFile>(MockBehavior.Strict);
                mockPackageFile.Setup(x => x.EffectivePath).Returns("myLibFxSample.dLl");
                mockPackageFile.Setup(x => x.Path).Returns(@"Lib\dnx451\myLibFxSample.dLl");
                mockPackageFile.Setup(x => x.SupportedFrameworks).Returns(
                        new List<FrameworkName>() {                     
                            VersionUtility.ParseFrameworkName("net45"),
                            VersionUtility.ParseFrameworkName("dnx451"),
                        });

                PackageFiles.Add(mockPackageFile);

                FileSystem.Setup(x => x.FileExists(Path.Combine(Path.GetDirectoryName(NupkgFile),
                                                                @"Lib\dnx451\myLibFxSample.dLl"))).Returns(true);
                var packageFiles = new List<IPackageFile>
                {
                    mockPackageFile.Object
                };

                var assemblyMetadata = new AssemblyMetadata
                {
                    FullPath = Path.Combine(Path.GetDirectoryName(NupkgFile),
                                            @"Lib\dnx451\myLibFxSample.dLl"),
                    TargetFrameworks = new[] { "net45", "dnx451" }
                };

                PackageAssemblyMetadata.Add(assemblyMetadata);

                return this;
            }

            public MockGenerator MockPackageInfo()
            {
                var packageId = Path.GetFileNameWithoutExtension(NupkgFile);
                NugetHelper.Setup(x => x.GetPackageFiles()).Returns(PackageFiles.Select(x => x.Object));
                NugetHelper.Setup(x => x.GetPackageSupportedFrameworks()).Returns(PackageFrameworks);
                NugetHelper.Setup(x => x.GetPackageId()).Returns(packageId);
                NugetHelper.Setup(x => x.GetPackageVersion()).Returns("1.0.0.0");

                return this;
            }

            public string SourceDirectory = @"d:\somefolder\SomeSubFolder";
            public MockGenerator MockGetPackages()
            {
                FileSystem.Setup(x => x.DirectoryExists(SourceDirectory)).Returns(true);
                FileSystem.Setup(x => x.DirectoryGetFiles(SourceDirectory, "*.nupkg", SearchOption.AllDirectories))
                          .Returns(new [] { NupkgFile });

                return this;
            }

            public MockGenerator MockNupkgNotFound()
            {
                FileSystem.Setup(x => x.FileExists(NupkgFile)).Returns(false);

                return this;
            }

            public PackageMetadata GetExpectedPackageMetadata()
            {
                var packageId = Path.GetFileNameWithoutExtension(NupkgFile);
                return new PackageMetadata
                {
                    Id = packageId,
                    Version = "1.0.0.0",
                    LocalPath = NupkgFile,
                    TargetFrameworks = PackageFrameworks.Select(x => VersionUtility.GetShortFrameworkName(x)),
                    Assemblies = PackageAssemblyMetadata
                };
            }
        }
    }
}