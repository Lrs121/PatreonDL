﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using NLog;
using PatreonDownloader.Common.Interfaces;
using PatreonDownloader.PuppeteerEngine;
using UniversalDownloaderPlatform.Common.Exceptions;
using UniversalDownloaderPlatform.Common.Interfaces;
using UniversalDownloaderPlatform.Common.Interfaces.Models;
using UniversalDownloaderPlatform.DefaultImplementations;
using UniversalDownloaderPlatform.DefaultImplementations.Interfaces;

namespace PatreonDownloader.Implementation
{
    internal class PatreonWebDownloader : WebDownloader
    {
        private readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private string _proxyServerAddress;

        public PatreonWebDownloader(IRemoteFileSizeChecker remoteFileSizeChecker) : base(remoteFileSizeChecker)
        {

        }

        public override Task BeforeStart(IUniversalDownloaderPlatformSettings settings)
        {
            _proxyServerAddress = settings.ProxyServerAddress;
            return base.BeforeStart(settings);
        }

        public override async Task DownloadFile(string url, string path, bool overwrite = false)
        {
            try
            {
                await base.DownloadFile(url, path, overwrite);
            }
            catch (DownloadException ex)
            {
                if (ex.StatusCode == HttpStatusCode.Forbidden && !string.IsNullOrWhiteSpace(ex.Response))
                {
                    if (ex.Response.ToLowerInvariant().Contains("https://ct.captcha-delivery.com/c.js"))
                    {
                        if (! await SolveCaptchaAndUpdateCookies(url))
                            throw;

                        await this.DownloadFile(url, path, overwrite);
                    }
                }
                else
                {
                    throw;
                }
            }
        }

        public override async Task<string> DownloadString(string url)
        {
            try
            {
                return await base.DownloadString(url);
            }
            catch (DownloadException ex)
            {
                if (ex.StatusCode == HttpStatusCode.Forbidden && !string.IsNullOrWhiteSpace(ex.Response))
                {
                    if (ex.Response.ToLowerInvariant().Contains("https://ct.captcha-delivery.com/c.js"))
                    {
                        if (!await SolveCaptchaAndUpdateCookies(url))
                            throw;

                        return await this.DownloadString(url);
                    }

                    throw;
                }
                else
                {
                    throw;
                }
            }
        }

        private async Task<bool> SolveCaptchaAndUpdateCookies(string url)
        {
            _logger.Warn("Captcha has been triggered, the browser window will be opened now. Please solve the captcha there.");

            PuppeteerCaptchaSolver captchaSolver = new PuppeteerCaptchaSolver(_proxyServerAddress);
            CookieCollection cookieCollection = await captchaSolver.SolveCaptcha(url);
            captchaSolver.Dispose();
            captchaSolver = null;

            if (cookieCollection == null)
                return false;

            UpdateCookies(cookieCollection);

            return true;
        }
    }
}
