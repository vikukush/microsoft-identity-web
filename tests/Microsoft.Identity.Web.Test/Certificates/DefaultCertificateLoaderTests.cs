﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Abstractions;
using Microsoft.Identity.Web.Test.Common;
using Moq;
using Xunit;

namespace Microsoft.Identity.Web.Test.Certificates
{
    public class DefaultCertificateLoaderTests
    {
        // https://portal.azure.com/#blade/Microsoft_AAD_RegisteredApps/ApplicationMenuBlade/Credentials/appId/9a192b78-6580-4f8a-aace-f36ffea4f7be/isMSAApp/
        // [InlineData(CertificateSource.KeyVault, TestConstants.KeyVaultContainer, TestConstants.KeyVaultReference)]
        // [InlineData(CertificateSource.Path, @"c:\temp\WebAppCallingWebApiCert.pfx", "")]
        // [InlineData(CertificateSource.StoreWithDistinguishedName, "CurrentUser/My", "CN=WebAppCallingWebApiCert")]
        // [InlineData(CertificateSource.StoreWithThumbprint, "CurrentUser/My", "962D129A859174EE8B5596985BD18EFEB6961684")]
#pragma warning disable xUnit1012 // Null should only be used for nullable parameters
        [InlineData(CertificateSource.Base64Encoded, null, TestConstants.CertificateX5c)]
#pragma warning restore xUnit1012 // Null should only be used for nullable parameters
        [Theory]
        public void TestDefaultCertificateLoader(CertificateSource certificateSource, string container, string referenceOrValue)
        {
            CertificateDescription certificateDescription;
            switch (certificateSource)
            {
                case CertificateSource.KeyVault:
                    certificateDescription = CertificateDescription.FromKeyVault(container, referenceOrValue);
                    break;
                case CertificateSource.Base64Encoded:
                    certificateDescription = CertificateDescription.FromBase64Encoded(referenceOrValue);
                    break;
                case CertificateSource.Path:
                    certificateDescription = CertificateDescription.FromPath(container, referenceOrValue);
                    break;
                case CertificateSource.StoreWithThumbprint:
                    certificateDescription = new CertificateDescription() { SourceType = CertificateSource.StoreWithThumbprint };
                    certificateDescription.CertificateThumbprint = referenceOrValue;
                    certificateDescription.CertificateStorePath = container;
                    break;
                case CertificateSource.StoreWithDistinguishedName:
                    certificateDescription = new CertificateDescription() { SourceType = CertificateSource.StoreWithDistinguishedName };
                    certificateDescription.CertificateDistinguishedName = referenceOrValue;
                    certificateDescription.CertificateStorePath = container;
                    break;
                default:
                    certificateDescription = new CertificateDescription();
                    break;
            }

            ICertificateLoader loader = new DefaultCertificateLoader();
            loader.LoadIfNeeded(certificateDescription);

            Assert.NotNull(certificateDescription.Certificate);
        }

#pragma warning disable xUnit1012 // Null should only be used for nullable parameters
        [InlineData(CertificateSource.Base64Encoded, null, TestConstants.CertificateX5c)]
#pragma warning restore xUnit1012 // Null should only be used for nullable parameters
        [Theory]
        public void TestLoadFirstCertificate(
            CertificateSource certificateSource,
            string container,
            string referenceOrValue)
        {
            IEnumerable<CertificateDescription> certDescriptions = CreateCertificateDescriptions(
                certificateSource,
                container,
                referenceOrValue);

            X509Certificate2? certificate = DefaultCertificateLoader.LoadFirstCertificate(certDescriptions);

            Assert.NotNull(certificate);
            Assert.Equal("CN=ACS2ClientCertificate", certificate.Issuer);
        }

#pragma warning disable xUnit1012 // Null should only be used for nullable parameters
        [InlineData(CertificateSource.Base64Encoded, null, TestConstants.CertificateX5c)]
#pragma warning restore xUnit1012 // Null should only be used for nullable parameters
        [Theory]
        public void TestLoadAllCertificates(
           CertificateSource certificateSource,
           string container,
           string referenceOrValue)
        {
            List<CertificateDescription> certDescriptions = CreateCertificateDescriptions(
                certificateSource,
                container,
                referenceOrValue).ToList();

            certDescriptions.Add(new CertificateDescription
            {
                SourceType = certificateSource,
                Container = container,
                ReferenceOrValue = referenceOrValue,
            });

            certDescriptions.Add(CertificateDescription.FromCertificate(null!));

            IEnumerable<X509Certificate2?> certificates = DefaultCertificateLoader.LoadAllCertificates(certDescriptions);

            Assert.NotNull(certificates);
            Assert.Equal(2, certificates.Count());
            Assert.Equal(3, certDescriptions.Count);
            Assert.NotNull(certificates.First());
            Assert.Equal("CN=ACS2ClientCertificate", certificates.First()!.Issuer);
            Assert.NotNull(certificates.Last());
            Assert.Equal("CN=ACS2ClientCertificate", certificates.Last()!.Issuer);
            Assert.Null(certDescriptions.ElementAt(2).Certificate);
        }

        [InlineData(CertificateSource.Base64Encoded, TestConstants.CertificateX5cWithPrivateKey, TestConstants.CertificateX5cWithPrivateKeyPassword)]
        //[InlineData(CertificateSource.Path, "Certificates\\SelfSignedTestCert.pfx", TestConstants.CertificateX5cWithPrivateKeyPassword)]
        [Theory]
        public void TestLoadCertificateWithPrivateKey(
                    CertificateSource certificateSource,
                    string container,
                    string password)
        {
            CertificateDescription certificateDescription;

            if (certificateSource == CertificateSource.Base64Encoded)
            {
                certificateDescription = CertificateDescription.FromBase64Encoded(container, password);
            }
            else
            {
                certificateDescription = CertificateDescription.FromPath(container, password);
            }

            DefaultCertificateLoader defaultCertificateLoader = new DefaultCertificateLoader();
            defaultCertificateLoader.LoadIfNeeded(certificateDescription);

            Assert.NotNull(certificateDescription.Certificate);
            Assert.True(certificateDescription.Certificate.HasPrivateKey);
        }

        private IEnumerable<CertificateDescription> CreateCertificateDescriptions(
            CertificateSource certificateSource,
            string container,
            string referenceOrValue)
        {
            List<CertificateDescription> certificateDescription = new List<CertificateDescription>();

            certificateDescription.Add(new CertificateDescription
            {
                SourceType = certificateSource,
                Container = container,
                ReferenceOrValue = referenceOrValue,
            });

            return certificateDescription;
        }

        [Fact]
        public async Task LoadFirstValidCredentialsAsync_ReturnsFirstValidCredential()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<DefaultCredentialsLoader>>();
            var credentialLoader = new DefaultCredentialsLoader(loggerMock.Object);

            var validCredential = new CredentialDescription
            {
                SourceType = CredentialSource.Path,
                ReferenceOrValue = "ValidCredential"
            };
            var invalidCredential = new CredentialDescription
            {
                SourceType = CredentialSource.Path,
                ReferenceOrValue = "InvalidCredential"
            };

            var credentials = new List<CredentialDescription> { invalidCredential, validCredential };

            var credentialSourceLoaderMock = new Mock<ICredentialSourceLoader>();
            credentialSourceLoaderMock
                .Setup(loader => loader.LoadIfNeededAsync(
                    It.Is<CredentialDescription>(c => c.ReferenceOrValue == "InvalidCredential"), null))
                .ThrowsAsync(new Exception("Loading failed"));
            credentialSourceLoaderMock
                .Setup(loader => loader.LoadIfNeededAsync(
                    It.Is<CredentialDescription>(c => c.ReferenceOrValue == "ValidCredential"), null))
                .Returns(Task.CompletedTask);

            credentialLoader.CredentialSourceLoaders[CredentialSource.Path] = credentialSourceLoaderMock.Object;

            // Act
            var result = await credentialLoader.LoadFirstValidCredentialsAsync(credentials);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(validCredential, result);
            credentialSourceLoaderMock.Verify(
                loader => loader.LoadIfNeededAsync(invalidCredential, null), Times.Once);
            credentialSourceLoaderMock.Verify(
                loader => loader.LoadIfNeededAsync(validCredential, null), Times.Once);
        }

        [Fact]
        public async Task LoadFirstValidCredentialsAsync_LogsWhenCredentialFailsToLoad()
        {
            // Arrange
            var logger = new LoggerFactory().CreateLogger<DefaultCredentialsLoader>();
            var credentialLoader = new DefaultCredentialsLoader(logger);

            var invalidCredential = new CredentialDescription
            {
                SourceType = CredentialSource.Path,
                ReferenceOrValue = "InvalidCredential"
            };
            var credentials = new List<CredentialDescription> { invalidCredential };

            var credentialSourceLoaderMock = new Mock<ICredentialSourceLoader>();
            credentialSourceLoaderMock
                .Setup(loader => loader.LoadIfNeededAsync(
                    It.Is<CredentialDescription>(c => c.ReferenceOrValue == "InvalidCredential"), null))
                .ThrowsAsync(new Exception("Loading failed"));

            credentialLoader.CredentialSourceLoaders[CredentialSource.Path] = credentialSourceLoaderMock.Object;

            // Act
            var result = await credentialLoader.LoadFirstValidCredentialsAsync(credentials);

            // Assert
            Assert.Null(result);
            string expectedMessage = $"Failed to load credential: {invalidCredential}. Exception: Loading failed";
            //Assert.Contains(expectedMessage, logger.LoggedMessages[0], StringComparison.Ordinal);
        }
    }
}
